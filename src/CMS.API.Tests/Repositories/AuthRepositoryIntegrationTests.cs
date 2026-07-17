using System.Security.Cryptography;
using System.Text;
using CMS.API.Infrastructure;
using CMS.API.Repositories;
using CMS.API.Tests.Infrastructure;
using Dapper;

namespace CMS.API.Tests.Repositories;

/// <summary>
/// Runs real SQL against the real CMS database. The credential check spans three columns
/// (UserId / IsActive / PasswordHash) plus the AppUserRole join — a mocked Dapper would hide a wrong
/// column or a mismatched hash. Everything this test owns is TEST_-prefixed and swept by the fixture.
///
/// GetSigningKey reads SysConfig 'appConfig'; if that row is absent this test seeds a throwaway one
/// (with a symmetricSecurityKey) and removes it, never mutating a pre-existing config.
///
/// Run without a DB:  dotnet test --filter "Category!=Integration"
/// </summary>
[Trait("Category", "Integration")]
[Collection(DatabaseCollection.Name)]
public class AuthRepositoryIntegrationTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private const string ValidPassword = "Secret#123";

    private readonly AuthRepository _repository = new(fixture.ConnectionFactory, fixture.AuditWriter);
    private readonly List<string> _createdUsers = [];
    private readonly List<string> _createdRoles = [];
    private bool _seededAppConfig;

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (!DatabaseProbe.IsAvailable) return;

        await using var conn = await fixture.OpenAsync();
        foreach (var userId in _createdUsers)
        {
            await conn.ExecuteAsync("DELETE FROM dbo.AppUserRole WHERE UserId = @UserId;", new { UserId = userId });
            await conn.ExecuteAsync("DELETE FROM dbo.AppUser WHERE UserId = @UserId;", new { UserId = userId });
        }
        foreach (var roleId in _createdRoles)
            await conn.ExecuteAsync("DELETE FROM dbo.AppRole WHERE RoleId = @RoleId;", new { RoleId = roleId });

        if (_seededAppConfig)
            await conn.ExecuteAsync("DELETE FROM dbo.SysConfig WHERE configKey = 'appConfig';");
    }

    /// <summary>A collision-proof UserId that also satisfies ^[^\s/\\]+$.</summary>
    private static string NewUserId() => $"{DatabaseFixture.Prefix}{Guid.NewGuid():N}"[..24];

    /// <summary>Seeds an AppUser whose PasswordHash is a current, salted hash of <paramref name="password"/>.</summary>
    private Task<string> SeedUserAsync(
        string password = ValidPassword, bool isActive = true, IReadOnlyList<string>? roleIds = null)
        => SeedUserWithHashAsync(PasswordHasher.Hash(password), isActive, roleIds);

    /// <summary>
    /// Seeds an AppUser with an EXACT stored hash. Lets a test plant a pre-2026-07-17 unsalted SHA-256
    /// digest and prove the legacy login path against real SQL.
    /// </summary>
    private async Task<string> SeedUserWithHashAsync(
        string passwordHash, bool isActive = true, IReadOnlyList<string>? roleIds = null)
    {
        var userId = NewUserId();
        _createdUsers.Add(userId);

        await using var conn = await fixture.OpenAsync();
        await conn.ExecuteAsync(
            """
            INSERT INTO dbo.AppUser (UserId, UserName, IsActive, PasswordHash, PasswordUpdatedTime)
            VALUES (@UserId, @UserName, @IsActive, @PasswordHash, SYSDATETIME());
            """,
            new
            {
                UserId = userId,
                UserName = $"User {userId}",
                IsActive = isActive,
                PasswordHash = passwordHash
            });

        foreach (var roleId in roleIds ?? [])
            await conn.ExecuteAsync(
                "INSERT INTO dbo.AppUserRole (UserId, RoleId) VALUES (@UserId, @RoleId);",
                new { UserId = userId, RoleId = roleId });

        return userId;
    }

    /// <summary>A digest in exactly the format the pre-2026-07-17 PasswordHasher wrote.</summary>
    private static string LegacySha256Of(string password) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(password)));

    private async Task<string?> ReadHashAsync(string userId)
    {
        await using var conn = await fixture.OpenAsync();
        return await conn.ExecuteScalarAsync<string?>(
            "SELECT PasswordHash FROM dbo.AppUser WHERE UserId = @UserId;", new { UserId = userId });
    }

    // ---------- Legacy SHA-256 migration ----------

    [IntegrationFact]
    public async Task AuthenticateAsync_AcceptsALegacySha256Row_AndUpgradesItInPlace()
    {
        // Every AppUser row written before 2026-07-17 looks exactly like this. If this test fails,
        // the hashing change locked every existing user out of the system.
        var userId = await SeedUserWithHashAsync(LegacySha256Of(ValidPassword));
        Assert.Equal(64, (await ReadHashAsync(userId))!.Length); // precondition: really is legacy

        var user = await _repository.AuthenticateAsync(userId, ValidPassword);

        Assert.NotNull(user);
        Assert.Equal(userId, user.UserId);

        // The successful login silently re-hashed the row — no reset, no user action.
        var upgraded = await ReadHashAsync(userId);
        Assert.StartsWith("pbkdf2-sha256$", upgraded);
        Assert.False(PasswordHasher.NeedsRehash(upgraded));

        // ...and the same password still works against the upgraded row on the next login.
        Assert.NotNull(await _repository.AuthenticateAsync(userId, ValidPassword));
    }

    [IntegrationFact]
    public async Task AuthenticateAsync_RejectsAWrongPasswordAgainstALegacyRow_AndLeavesItAlone()
    {
        var userId = await SeedUserWithHashAsync(LegacySha256Of(ValidPassword));

        Assert.Null(await _repository.AuthenticateAsync(userId, "wrong-password"));

        // A failed login must never rewrite the stored hash.
        Assert.Equal(64, (await ReadHashAsync(userId))!.Length);
    }

    [IntegrationFact]
    public async Task AuthenticateAsync_DoesNotUpgrade_AnInactiveLegacyUser()
    {
        // IsActive = 0 is filtered in the WHERE, so no row comes back to verify or upgrade.
        var userId = await SeedUserWithHashAsync(LegacySha256Of(ValidPassword), isActive: false);

        Assert.Null(await _repository.AuthenticateAsync(userId, ValidPassword));
        Assert.Equal(64, (await ReadHashAsync(userId))!.Length);
    }

    [IntegrationFact]
    public async Task AuthenticateAsync_LeavesAnAlreadyCurrentHashUntouched()
    {
        // Only stale hashes get rewritten — a normal login must not write to the DB.
        var userId = await SeedUserAsync();
        var before = await ReadHashAsync(userId);

        Assert.NotNull(await _repository.AuthenticateAsync(userId, ValidPassword));

        Assert.Equal(before, await ReadHashAsync(userId));
    }

    private async Task<string> SeedRoleAsync()
    {
        var roleId = DatabaseFixture.NewRoleId();
        _createdRoles.Add(roleId);

        await using var conn = await fixture.OpenAsync();
        await conn.ExecuteAsync(
            "INSERT INTO dbo.AppRole (RoleId, RoleName, PermissionLevel) VALUES (@RoleId, @RoleName, 100);",
            new { RoleId = roleId, RoleName = $"Role {roleId}" });
        return roleId;
    }

    // ---------- AuthenticateAsync ----------

    [IntegrationFact]
    public async Task AuthenticateAsync_ReturnsUserWithRoles_ForValidActiveCredentials()
    {
        var roleA = await SeedRoleAsync();
        var roleB = await SeedRoleAsync();
        var userId = await SeedUserAsync(roleIds: [roleA, roleB]);

        var result = await _repository.AuthenticateAsync(userId, ValidPassword);

        Assert.NotNull(result);
        Assert.Equal(userId, result.UserId);
        Assert.Equal($"User {userId}", result.UserName);
        Assert.Equal(new[] { roleA, roleB }.OrderBy(x => x), result.RoleIds.OrderBy(x => x));
    }

    [IntegrationFact]
    public async Task AuthenticateAsync_ReturnsNull_ForWrongPassword()
    {
        var userId = await SeedUserAsync();

        Assert.Null(await _repository.AuthenticateAsync(userId, "not-the-password"));
    }

    [IntegrationFact]
    public async Task AuthenticateAsync_ReturnsNull_ForUnknownUserId()
    {
        Assert.Null(await _repository.AuthenticateAsync("TEST_no_such_user", ValidPassword));
    }

    [IntegrationFact]
    public async Task AuthenticateAsync_ReturnsNull_ForInactiveUser()
    {
        // Correct password, but IsActive = 0 — the account must not authenticate.
        var userId = await SeedUserAsync(isActive: false);

        Assert.Null(await _repository.AuthenticateAsync(userId, ValidPassword));
    }

    [IntegrationFact]
    public async Task AuthenticateAsync_ReturnsNull_ForBlankInput()
    {
        Assert.Null(await _repository.AuthenticateAsync("", ""));
    }

    // ---------- VerifyPasswordAsync ----------

    [IntegrationFact]
    public async Task VerifyPasswordAsync_True_ForCorrectPassword()
    {
        var userId = await SeedUserAsync();

        Assert.True(await _repository.VerifyPasswordAsync(userId, ValidPassword));
    }

    [IntegrationFact]
    public async Task VerifyPasswordAsync_False_ForWrongPasswordOrUnknownUser()
    {
        var userId = await SeedUserAsync();

        Assert.False(await _repository.VerifyPasswordAsync(userId, "not-the-password"));
        Assert.False(await _repository.VerifyPasswordAsync("TEST_no_such_user", ValidPassword));
        Assert.False(await _repository.VerifyPasswordAsync(userId, ""));
    }

    // ---------- UpdatePasswordAsync ----------

    [IntegrationFact]
    public async Task UpdatePasswordAsync_StoresASaltedHashOfNew_AndBumpsUpdatedTime()
    {
        var userId = await SeedUserAsync();
        var (_, originalTime) = await ReadPasswordAsync(userId);
        const string newPassword = "Brand#New9";

        var ok = await _repository.UpdatePasswordAsync(userId, newPassword);

        Assert.True(ok);
        var (storedHash, updatedTime) = await ReadPasswordAsync(userId);

        // Asserted by verifying, not by comparing to a re-computed hash: the hash is salted, so
        // Hash(newPassword) yields a DIFFERENT string every call and equality would always fail.
        // What matters is the behaviour, not the bytes.
        Assert.True(PasswordHasher.Verify(newPassword, storedHash));
        // ...and the stored value is a real salted hash, not the plaintext or a bare digest.
        Assert.NotEqual(newPassword, storedHash);
        Assert.StartsWith("pbkdf2-sha256$", storedHash);
        // The new password now verifies and the old one no longer does.
        Assert.True(await _repository.VerifyPasswordAsync(userId, newPassword));
        Assert.False(await _repository.VerifyPasswordAsync(userId, ValidPassword));
        // PasswordUpdatedTime was stamped to (roughly) now, at or after the seed time.
        Assert.NotNull(updatedTime);
        Assert.True(updatedTime >= originalTime);
        Assert.True(Math.Abs((DateTime.Now - updatedTime!.Value).TotalMinutes) < 5);
    }

    [IntegrationFact]
    public async Task UpdatePasswordAsync_False_ForUnknownUser()
    {
        Assert.False(await _repository.UpdatePasswordAsync("TEST_no_such_user", "Brand#New9"));
    }

    /// <summary>Reads the raw PasswordHash / PasswordUpdatedTime for a test-owned user.</summary>
    private async Task<(string? Hash, DateTime? UpdatedTime)> ReadPasswordAsync(string userId)
    {
        await using var conn = await fixture.OpenAsync();
        var row = await conn.QuerySingleAsync<(string? Hash, DateTime? UpdatedTime)>(
            "SELECT PasswordHash AS Hash, PasswordUpdatedTime AS UpdatedTime FROM dbo.AppUser WHERE UserId = @UserId;",
            new { UserId = userId });
        return row;
    }

    // ---------- GetSigningKeyAsync ----------

    [IntegrationFact]
    public async Task GetSigningKeyAsync_ReturnsConfiguredKey()
    {
        await EnsureAppConfigAsync();

        var key = await _repository.GetSigningKeyAsync();

        Assert.False(string.IsNullOrWhiteSpace(key));
    }

    /// <summary>Seeds appConfig (with a symmetricSecurityKey) only when it is missing.</summary>
    private async Task EnsureAppConfigAsync()
    {
        await using var conn = await fixture.OpenAsync();
        var existing = await conn.ExecuteScalarAsync<string?>(
            "SELECT configValue FROM dbo.SysConfig WHERE configKey = 'appConfig';");

        if (!string.IsNullOrWhiteSpace(existing)) return;

        await conn.ExecuteAsync(
            "INSERT INTO dbo.SysConfig (configKey, configValue) VALUES ('appConfig', @Value);",
            new
            {
                Value =
                    """{"defaultPassword":"Test@Default#1","symmetricSecurityKey":"integration-test-signing-key-0123456789-abcdefghij"}"""
            });
        _seededAppConfig = true;
    }
}
