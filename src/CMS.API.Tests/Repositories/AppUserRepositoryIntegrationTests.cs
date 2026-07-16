using CMS.API.Models;
using CMS.API.Repositories;
using CMS.API.Tests.Infrastructure;
using Dapper;
using Microsoft.Data.SqlClient;

namespace CMS.API.Tests.Repositories;

/// <summary>
/// Runs real SQL against the real CMS database. Mocking Dapper would prove nothing — a wrong join
/// column, a broken ESCAPE clause, or a PasswordHash that never gets set is invisible to a mocked
/// test and only fails in production.
///
/// AppUser's create path reads the default password from SysConfig ('appConfig'). If that row is
/// absent on the test box, this fixture seeds a throwaway one and removes it again — it never mutates
/// a pre-existing appConfig. TEST_ AppRoles are seeded for the n-n; the DatabaseFixture sweep clears
/// TEST_ AppUser / AppRole / AppUserRole rows.
///
/// Run without a DB:  dotnet test --filter "Category!=Integration"
/// </summary>
[Trait("Category", "Integration")]
[Collection(DatabaseCollection.Name)]
public class AppUserRepositoryIntegrationTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private readonly AppUserRepository _repository = new(fixture.ConnectionFactory, fixture.AuditWriter);
    private readonly List<string> _createdUsers = [];
    private readonly List<string> _createdRoles = [];
    private bool _seededAppConfig;

    private string _roleA = string.Empty;
    private string _roleB = string.Empty;

    public async Task InitializeAsync()
    {
        if (!DatabaseProbe.IsAvailable) return;

        await EnsureAppConfigAsync();

        // Two TEST_ roles to assign through the AppUserRole n-n.
        _roleA = await SeedRoleAsync();
        _roleB = await SeedRoleAsync();
    }

    public async Task DisposeAsync()
    {
        if (!DatabaseProbe.IsAvailable) return;

        foreach (var userId in _createdUsers)
            await _repository.DeleteAsync(userId);

        await using var conn = await fixture.OpenAsync();
        foreach (var roleId in _createdRoles)
            await conn.ExecuteAsync("DELETE FROM dbo.AppRole WHERE RoleId = @RoleId;", new { RoleId = roleId });

        if (_seededAppConfig)
            await conn.ExecuteAsync("DELETE FROM dbo.SysConfig WHERE configKey = 'appConfig';");
    }

    /// <summary>Only seeds appConfig when it is missing — a pre-existing production value is left alone.</summary>
    private async Task EnsureAppConfigAsync()
    {
        await using var conn = await fixture.OpenAsync();
        var existing = await conn.ExecuteScalarAsync<string?>(
            "SELECT configValue FROM dbo.SysConfig WHERE configKey = 'appConfig';");

        if (!string.IsNullOrWhiteSpace(existing)) return;

        await conn.ExecuteAsync(
            "INSERT INTO dbo.SysConfig (configKey, configValue) VALUES ('appConfig', @Value);",
            new { Value = """{"defaultPassword":"Test@Default#1"}""" });
        _seededAppConfig = true;
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

    /// <summary>A collision-proof UserId that also satisfies ^[^\s/\\]+$.</summary>
    private static string NewUserId() => $"{DatabaseFixture.Prefix}{Guid.NewGuid():N}"[..24];

    /// <summary>Builds a request with a unique UserId and registers it for cleanup.</summary>
    private AppUserRequest NewRequest(
        string? userName = null, bool isActive = true, List<string>? roleIds = null)
    {
        var userId = NewUserId();
        _createdUsers.Add(userId);
        return new AppUserRequest
        {
            UserId = userId,
            UserName = userName ?? $"User {userId}",
            IsActive = isActive,
            RoleIds = roleIds ?? []
        };
    }

    private async Task<IReadOnlyList<string>> ReadRoleIdsAsync(string userId)
    {
        await using var conn = await fixture.OpenAsync();
        var rows = await conn.QueryAsync<string>(
            "SELECT RoleId FROM dbo.AppUserRole WHERE UserId = @UserId ORDER BY RoleId;",
            new { UserId = userId });
        return rows.AsList();
    }

    private async Task<string?> ReadPasswordHashAsync(string userId)
    {
        await using var conn = await fixture.OpenAsync();
        return await conn.ExecuteScalarAsync<string?>(
            "SELECT PasswordHash FROM dbo.AppUser WHERE UserId = @UserId;", new { UserId = userId });
    }

    // ---------- GetAll ----------

    [IntegrationFact]
    public async Task GetAllAsync_IncludesSeededUsers_OrderedByUserId()
    {
        // The fixture seeds TEST_user_a / TEST_user_b.
        var users = await _repository.GetAllAsync();

        Assert.Contains(users, u => u.UserId == DatabaseFixture.TestUserIds[0]);

        var userIds = users.Select(u => u.UserId).ToList();
        Assert.Equal(userIds.OrderBy(x => x, StringComparer.OrdinalIgnoreCase), userIds);
    }

    [IntegrationFact]
    public async Task GetAllAsync_ProjectsRoleCount_FromJunctionTable()
    {
        var request = NewRequest(roleIds: [_roleA, _roleB]);
        await _repository.InsertAsync(request);

        var user = (await _repository.GetAllAsync()).Single(u => u.UserId == request.UserId);

        Assert.Equal(2, user.RoleCount);
    }

    // ---------- Query ----------

    [IntegrationFact]
    public async Task QueryAsync_Keyword_MatchesUserName()
    {
        var request = NewRequest(userName: "a ZZmarkerUserName here");
        await _repository.InsertAsync(request);

        var results = await _repository.QueryAsync(new AppUserQuery { Keyword = "ZZmarkerUserName" });

        Assert.Equal(request.UserId, Assert.Single(results).UserId);
    }

    [IntegrationFact]
    public async Task QueryAsync_Keyword_MatchesUserId()
    {
        var request = NewRequest();
        await _repository.InsertAsync(request);

        var results = await _repository.QueryAsync(new AppUserQuery { Keyword = request.UserId });

        Assert.Equal(request.UserId, Assert.Single(results).UserId);
    }

    [IntegrationFact]
    public async Task QueryAsync_Keyword_EscapesLikeWildcards()
    {
        var request = NewRequest(userName: "no wildcards here");
        await _repository.InsertAsync(request);

        // "%" must be a literal, not a wildcard — the precise bug ESCAPE '\' guards against.
        var percent = await _repository.QueryAsync(new AppUserQuery { Keyword = "%" });

        Assert.DoesNotContain(percent, u => u.UserId == request.UserId);
        Assert.Empty(percent);
    }

    [IntegrationFact]
    public async Task QueryAsync_IsActive_FiltersExactly()
    {
        var active = NewRequest(isActive: true);
        var inactive = NewRequest(isActive: false);
        await _repository.InsertAsync(active);
        await _repository.InsertAsync(inactive);

        var actives = await _repository.QueryAsync(new AppUserQuery { IsActive = true });
        var inactives = await _repository.QueryAsync(new AppUserQuery { IsActive = false });

        Assert.Contains(actives, u => u.UserId == active.UserId);
        Assert.DoesNotContain(actives, u => u.UserId == inactive.UserId);
        Assert.Contains(inactives, u => u.UserId == inactive.UserId);
        Assert.DoesNotContain(inactives, u => u.UserId == active.UserId);
    }

    // ---------- GetById ----------

    [IntegrationFact]
    public async Task GetByIdAsync_ReturnsNull_WhenMissing()
    {
        Assert.Null(await _repository.GetByIdAsync("TEST_nope"));
    }

    [IntegrationFact]
    public async Task GetByIdAsync_ReturnsUser_WithAssignedRoleIds()
    {
        var request = NewRequest(roleIds: [_roleB, _roleA]);
        await _repository.InsertAsync(request);

        var user = await _repository.GetByIdAsync(request.UserId);

        Assert.NotNull(user);
        Assert.Equal(request.UserName, user.UserName);
        Assert.Equal(new[] { _roleA, _roleB }.OrderBy(x => x), user.RoleIds.OrderBy(x => x));
        Assert.Equal(2, user.RoleCount);
    }

    // ---------- Insert ----------

    [IntegrationFact]
    public async Task InsertAsync_ReturnsIdentityPkid_AndHashesDefaultPassword()
    {
        var request = NewRequest();

        var pkid = await _repository.InsertAsync(request);

        Assert.True(pkid > 0);
        var user = await _repository.GetByIdAsync(request.UserId);
        Assert.NotNull(user);
        Assert.Equal(pkid, user.Pkid);
        Assert.NotNull(user.PasswordUpdatedTime);   // stamped on create

        // PasswordHash is set backend-side and never surfaces in the model — read it raw.
        var hash = await ReadPasswordHashAsync(request.UserId);
        Assert.False(string.IsNullOrEmpty(hash));
        Assert.Equal(64, hash!.Length);             // SHA-256 lowercase hex
        Assert.Matches("^[0-9a-f]{64}$", hash);
    }

    [IntegrationFact]
    public async Task InsertAsync_DedupesDuplicateRoleIdsInPayload()
    {
        var request = NewRequest(roleIds: [_roleA, _roleA]);

        await _repository.InsertAsync(request);

        Assert.Equal([_roleA], await ReadRoleIdsAsync(request.UserId));
    }

    [IntegrationFact]
    public async Task InsertAsync_IgnoresBlankRoleIds()
    {
        var request = NewRequest(roleIds: [_roleA, "", "   "]);

        await _repository.InsertAsync(request);

        Assert.Equal([_roleA], await ReadRoleIdsAsync(request.UserId));
    }

    [IntegrationFact]
    public async Task InsertAsync_ThrowsDuplicateKey_OnExistingUserId()
    {
        var request = NewRequest();
        await _repository.InsertAsync(request);

        var ex = await Assert.ThrowsAsync<SqlException>(() => _repository.InsertAsync(request));

        Assert.Contains(ex.Number, new[] { 2627, 2601 });
    }

    [IntegrationFact]
    public async Task InsertAsync_RollsBackUser_WhenRoleIdViolatesForeignKey()
    {
        var request = NewRequest(roleIds: ["TEST_no_such_role"]);

        var ex = await Assert.ThrowsAsync<SqlException>(() => _repository.InsertAsync(request));

        Assert.Equal(547, ex.Number);
        // The user INSERT and the junction INSERT share one transaction — a failed role
        // assignment must not leave a half-created user behind.
        Assert.Null(await _repository.GetByIdAsync(request.UserId));
    }

    // ---------- Update ----------

    [IntegrationFact]
    public async Task UpdateAsync_UpdatesScalars_LeavingUserIdPkidAndPasswordUntouched()
    {
        var request = NewRequest(userName: "Before", isActive: true);
        var pkid = await _repository.InsertAsync(request);
        var hashBefore = await ReadPasswordHashAsync(request.UserId);
        var stampBefore = (await _repository.GetByIdAsync(request.UserId))!.PasswordUpdatedTime;

        request.UserName = "After";
        request.IsActive = false;
        var updated = await _repository.UpdateAsync(request);

        Assert.True(updated);
        var user = await _repository.GetByIdAsync(request.UserId);
        Assert.NotNull(user);
        Assert.Equal("After", user.UserName);
        Assert.False(user.IsActive);
        Assert.Equal(request.UserId, user.UserId);
        Assert.Equal(pkid, user.Pkid);
        // Update must not touch the password columns.
        Assert.Equal(hashBefore, await ReadPasswordHashAsync(request.UserId));
        Assert.Equal(stampBefore, user.PasswordUpdatedTime);
    }

    [IntegrationFact]
    public async Task UpdateAsync_ReplacesUserRoles_DeleteThenReinsert()
    {
        var request = NewRequest(roleIds: [_roleA]);
        await _repository.InsertAsync(request);
        Assert.Equal([_roleA], await ReadRoleIdsAsync(request.UserId));

        request.RoleIds = [_roleB];
        await _repository.UpdateAsync(request);
        Assert.Equal([_roleB], await ReadRoleIdsAsync(request.UserId));

        request.RoleIds = [];
        await _repository.UpdateAsync(request);
        Assert.Empty(await ReadRoleIdsAsync(request.UserId));
    }

    [IntegrationFact]
    public async Task UpdateAsync_ReturnsFalse_WhenUserIdMissing()
    {
        var request = new AppUserRequest { UserId = "TEST_nope", UserName = "X", IsActive = true };

        Assert.False(await _repository.UpdateAsync(request));
    }

    // ---------- Reset password ----------

    [IntegrationFact]
    public async Task ResetPasswordAsync_RestoresDefaultHash_AndReturnsTrueForExistingUser()
    {
        var request = NewRequest();
        await _repository.InsertAsync(request);
        var defaultHash = await ReadPasswordHashAsync(request.UserId);

        // Corrupt the stored hash so the reset is observable.
        await using (var conn = await fixture.OpenAsync())
        {
            await conn.ExecuteAsync(
                "UPDATE dbo.AppUser SET PasswordHash = 'corrupted' WHERE UserId = @UserId;",
                new { request.UserId });
        }
        Assert.NotEqual(defaultHash, await ReadPasswordHashAsync(request.UserId));

        var reset = await _repository.ResetPasswordAsync(request.UserId);

        Assert.True(reset);
        Assert.Equal(defaultHash, await ReadPasswordHashAsync(request.UserId));
    }

    [IntegrationFact]
    public async Task ResetPasswordAsync_ReturnsFalse_WhenMissing()
    {
        Assert.False(await _repository.ResetPasswordAsync("TEST_nope"));
    }

    // ---------- Delete ----------

    [IntegrationFact]
    public async Task DeleteAsync_RemovesUser_AndItsJunctionRows()
    {
        var request = NewRequest(roleIds: [_roleA, _roleB]);
        await _repository.InsertAsync(request);

        // FK_AppUserRole_AppUser has no ON DELETE CASCADE — deleting the user without clearing
        // the junction first would throw 547.
        var deleted = await _repository.DeleteAsync(request.UserId);

        Assert.True(deleted);
        Assert.Null(await _repository.GetByIdAsync(request.UserId));
        Assert.Empty(await ReadRoleIdsAsync(request.UserId));
    }

    [IntegrationFact]
    public async Task DeleteAsync_ReturnsFalse_WhenMissing()
    {
        Assert.False(await _repository.DeleteAsync("TEST_nope"));
    }

    // ---------- Exists ----------

    [IntegrationFact]
    public async Task ExistsAsync_ReflectsPresence()
    {
        var request = NewRequest();
        Assert.False(await _repository.ExistsAsync(request.UserId));

        await _repository.InsertAsync(request);

        Assert.True(await _repository.ExistsAsync(request.UserId));
    }
}
