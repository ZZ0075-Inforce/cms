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

    private readonly AuthRepository _repository = new(fixture.ConnectionFactory);
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

    /// <summary>Seeds an AppUser whose PasswordHash is the SHA-256 the repository will compare against.</summary>
    private async Task<string> SeedUserAsync(
        string password = ValidPassword, bool isActive = true, IReadOnlyList<string>? roleIds = null)
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
                PasswordHash = PasswordHasher.Hash(password)
            });

        foreach (var roleId in roleIds ?? [])
            await conn.ExecuteAsync(
                "INSERT INTO dbo.AppUserRole (UserId, RoleId) VALUES (@UserId, @RoleId);",
                new { UserId = userId, RoleId = roleId });

        return userId;
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
    public async Task UpdatePasswordAsync_SetsHashToSha256OfNew_AndBumpsUpdatedTime()
    {
        var userId = await SeedUserAsync();
        var (_, originalTime) = await ReadPasswordAsync(userId);
        const string newPassword = "Brand#New9";

        var ok = await _repository.UpdatePasswordAsync(userId, newPassword);

        Assert.True(ok);
        var (storedHash, updatedTime) = await ReadPasswordAsync(userId);

        // PasswordHash is exactly SHA-256(new).
        Assert.Equal(PasswordHasher.Hash(newPassword), storedHash);
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
