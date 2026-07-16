using CMS.API.Models;
using CMS.API.Repositories;
using CMS.API.Tests.Infrastructure;
using Dapper;
using Microsoft.Data.SqlClient;

namespace CMS.API.Tests.Repositories;

/// <summary>
/// Runs real SQL against the real CMS database. Mocking Dapper would prove nothing — a wrong join
/// column or a broken ESCAPE clause is invisible to a mocked test and only fails in production.
///
/// Run without a DB:  dotnet test --filter "Category!=Integration"
/// (Individually these also self-skip when SQLEXPRESS is unreachable — see IntegrationFact.)
/// </summary>
[Trait("Category", "Integration")]
[Collection(DatabaseCollection.Name)]
public class AppRoleRepositoryIntegrationTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private readonly AppRoleRepository _repository = new(fixture.ConnectionFactory, fixture.AuditWriter);
    private readonly List<string> _created = [];

    private static string UserA => DatabaseFixture.TestUserIds[0];
    private static string UserB => DatabaseFixture.TestUserIds[1];

    public Task InitializeAsync() => Task.CompletedTask;

    /// <summary>Removes only what this test created.</summary>
    public async Task DisposeAsync()
    {
        if (!DatabaseProbe.IsAvailable) return;

        foreach (var roleId in _created)
            await _repository.DeleteAsync(roleId);
    }

    /// <summary>Builds a request with a unique RoleId and registers it for cleanup.</summary>
    private AppRoleRequest NewRequest(
        string? roleName = null, int permissionLevel = 100,
        string? description = null, List<string>? userIds = null)
    {
        var roleId = DatabaseFixture.NewRoleId();
        _created.Add(roleId);
        return new AppRoleRequest
        {
            RoleId = roleId,
            RoleName = roleName ?? $"Role {roleId}",
            PermissionLevel = permissionLevel,
            Description = description,
            UserIds = userIds ?? []
        };
    }

    private async Task<IReadOnlyList<string>> ReadUserIdsAsync(string roleId)
    {
        await using var conn = await fixture.OpenAsync();
        var rows = await conn.QueryAsync<string>(
            "SELECT UserId FROM dbo.AppUserRole WHERE RoleId = @RoleId ORDER BY UserId;",
            new { RoleId = roleId });
        return rows.AsList();
    }

    // ---------- GetAll ----------

    [IntegrationFact]
    public async Task GetAllAsync_IncludesSeededRoles_OrderedByRoleId()
    {
        var roles = await _repository.GetAllAsync();

        // Containment, never an exact count — the table grows as the app does.
        Assert.Contains(roles, r => r.RoleId == "Admin");

        var roleIds = roles.Select(r => r.RoleId).ToList();
        Assert.Equal(roleIds.OrderBy(x => x, StringComparer.OrdinalIgnoreCase), roleIds);
    }

    [IntegrationFact]
    public async Task GetAllAsync_ProjectsUserCount_FromJunctionTable()
    {
        var request = NewRequest(userIds: [UserA, UserB]);
        await _repository.InsertAsync(request);

        var role = (await _repository.GetAllAsync()).Single(r => r.RoleId == request.RoleId);

        Assert.Equal(2, role.UserCount);
    }

    // ---------- Query ----------

    [IntegrationTheory]
    [InlineData("ZZmarkerName")]   // hits RoleName
    [InlineData("ZZmarkerDesc")]   // hits Description
    public async Task QueryAsync_Keyword_MatchesRoleNameAndDescription(string keyword)
    {
        var request = NewRequest(roleName: "a ZZmarkerName here", description: "a ZZmarkerDesc here");
        await _repository.InsertAsync(request);

        var results = await _repository.QueryAsync(new AppRoleQuery { Keyword = keyword });

        Assert.Equal(request.RoleId, Assert.Single(results).RoleId);
    }

    [IntegrationFact]
    public async Task QueryAsync_Keyword_MatchesRoleId()
    {
        var request = NewRequest();
        await _repository.InsertAsync(request);

        var results = await _repository.QueryAsync(new AppRoleQuery { Keyword = request.RoleId });

        Assert.Equal(request.RoleId, Assert.Single(results).RoleId);
    }

    [IntegrationFact]
    public async Task QueryAsync_Keyword_EscapesLikeWildcards()
    {
        var request = NewRequest(roleName: "no wildcards here", description: "none either");
        await _repository.InsertAsync(request);

        // "%" must be a literal, not a wildcard. Unescaped, this predicate degrades to
        // "match every row" — the precise bug ESCAPE '\' guards against.
        var percent = await _repository.QueryAsync(new AppRoleQuery { Keyword = "%" });

        Assert.DoesNotContain(percent, r => r.RoleId == request.RoleId);
        Assert.DoesNotContain(percent, r => r.RoleId == "Admin");
        Assert.Empty(percent);
    }

    [IntegrationFact]
    public async Task QueryAsync_Keyword_TreatsUnderscoreAsLiteral()
    {
        // "_" is the single-char LIKE wildcard. Escaped, it only matches a literal underscore —
        // which every TEST_ role has, but "Admin"/"User" do not.
        var request = NewRequest();
        await _repository.InsertAsync(request);

        var results = await _repository.QueryAsync(new AppRoleQuery { Keyword = "_" });

        Assert.Contains(results, r => r.RoleId == request.RoleId);
        Assert.DoesNotContain(results, r => r.RoleId == "Admin");
    }

    [IntegrationFact]
    public async Task QueryAsync_PermissionLevel_FiltersExactly()
    {
        var request = NewRequest(permissionLevel: 4321);
        await _repository.InsertAsync(request);

        var matching = await _repository.QueryAsync(new AppRoleQuery { PermissionLevel = 4321 });
        var other = await _repository.QueryAsync(new AppRoleQuery { PermissionLevel = 4322 });

        Assert.Contains(matching, r => r.RoleId == request.RoleId);
        Assert.DoesNotContain(other, r => r.RoleId == request.RoleId);
    }

    [IntegrationFact]
    public async Task QueryAsync_CombinesKeywordAndPermissionLevel()
    {
        var request = NewRequest(permissionLevel: 4321);
        await _repository.InsertAsync(request);

        var hit = await _repository.QueryAsync(
            new AppRoleQuery { Keyword = request.RoleId, PermissionLevel = 4321 });
        var miss = await _repository.QueryAsync(
            new AppRoleQuery { Keyword = request.RoleId, PermissionLevel = 1 });

        Assert.Single(hit);
        Assert.Empty(miss);
    }

    [IntegrationFact]
    public async Task QueryAsync_NoFilters_ReturnsEverything()
    {
        var request = NewRequest();
        await _repository.InsertAsync(request);

        var results = await _repository.QueryAsync(new AppRoleQuery());

        Assert.Contains(results, r => r.RoleId == request.RoleId);
        Assert.Contains(results, r => r.RoleId == "Admin");
    }

    // ---------- GetById ----------

    [IntegrationFact]
    public async Task GetByIdAsync_ReturnsNull_WhenMissing()
    {
        Assert.Null(await _repository.GetByIdAsync("TEST_nope"));
    }

    [IntegrationFact]
    public async Task GetByIdAsync_ReturnsRole_WithAssignedUserIds()
    {
        var request = NewRequest(description: "描述", userIds: [UserB, UserA]);
        await _repository.InsertAsync(request);

        var role = await _repository.GetByIdAsync(request.RoleId);

        Assert.NotNull(role);
        Assert.Equal(request.RoleName, role.RoleName);
        Assert.Equal("描述", role.Description);          // nvarchar round-trip
        Assert.Equal([UserA, UserB], role.UserIds);      // ordered by UserId
        Assert.Equal(2, role.UserCount);
    }

    // ---------- Insert ----------

    [IntegrationFact]
    public async Task InsertAsync_ReturnsIdentityPkid_AndPersistsRole()
    {
        var request = NewRequest(description: null);

        var pkid = await _repository.InsertAsync(request);

        Assert.True(pkid > 0);
        var role = await _repository.GetByIdAsync(request.RoleId);
        Assert.NotNull(role);
        Assert.Equal(pkid, role.Pkid);
        Assert.Null(role.Description);
    }

    [IntegrationFact]
    public async Task InsertAsync_DedupesDuplicateUserIdsInPayload()
    {
        // Without .Distinct() this violates PK_AppUserRole(UserId, RoleId) and throws 2627,
        // aborting the whole save.
        var request = NewRequest(userIds: [UserA, UserA]);

        await _repository.InsertAsync(request);

        Assert.Equal([UserA], await ReadUserIdsAsync(request.RoleId));
    }

    [IntegrationFact]
    public async Task InsertAsync_IgnoresBlankUserIds()
    {
        var request = NewRequest(userIds: [UserA, "", "   "]);

        await _repository.InsertAsync(request);

        Assert.Equal([UserA], await ReadUserIdsAsync(request.RoleId));
    }

    [IntegrationFact]
    public async Task InsertAsync_ThrowsDuplicateKey_OnExistingRoleId()
    {
        var request = NewRequest();
        await _repository.InsertAsync(request);

        var ex = await Assert.ThrowsAsync<SqlException>(() => _repository.InsertAsync(request));

        Assert.Contains(ex.Number, new[] { 2627, 2601 });
    }

    [IntegrationFact]
    public async Task InsertAsync_RollsBackRole_WhenUserIdViolatesForeignKey()
    {
        var request = NewRequest(userIds: ["TEST_no_such_user"]);

        var ex = await Assert.ThrowsAsync<SqlException>(() => _repository.InsertAsync(request));

        Assert.Equal(547, ex.Number);
        // The role INSERT and the junction INSERT share one transaction — a failed user
        // assignment must not leave a half-created role behind.
        Assert.Null(await _repository.GetByIdAsync(request.RoleId));
    }

    // ---------- Update ----------

    [IntegrationFact]
    public async Task UpdateAsync_UpdatesScalars_LeavingRoleIdAndPkidUntouched()
    {
        var request = NewRequest(roleName: "Before", permissionLevel: 10, description: "before");
        var pkid = await _repository.InsertAsync(request);

        request.RoleName = "After";
        request.PermissionLevel = 20;
        request.Description = null;
        var updated = await _repository.UpdateAsync(request);

        Assert.True(updated);
        var role = await _repository.GetByIdAsync(request.RoleId);
        Assert.NotNull(role);
        Assert.Equal("After", role.RoleName);
        Assert.Equal(20, role.PermissionLevel);
        Assert.Null(role.Description);
        Assert.Equal(request.RoleId, role.RoleId);
        Assert.Equal(pkid, role.Pkid);   // the IDENTITY must survive an update
    }

    [IntegrationFact]
    public async Task UpdateAsync_ReplacesUserRoles_DeleteThenReinsert()
    {
        var request = NewRequest(userIds: [UserA]);
        await _repository.InsertAsync(request);
        Assert.Equal([UserA], await ReadUserIdsAsync(request.RoleId));

        request.UserIds = [UserB];          // swap the assignment
        await _repository.UpdateAsync(request);
        Assert.Equal([UserB], await ReadUserIdsAsync(request.RoleId));

        request.UserIds = [];               // unassign everyone
        await _repository.UpdateAsync(request);
        Assert.Empty(await ReadUserIdsAsync(request.RoleId));
    }

    [IntegrationFact]
    public async Task UpdateAsync_ReturnsFalse_WhenRoleIdMissing()
    {
        var request = new AppRoleRequest
        {
            RoleId = "TEST_nope",
            RoleName = "X",
            PermissionLevel = 1
        };

        Assert.False(await _repository.UpdateAsync(request));
    }

    // ---------- Delete ----------

    [IntegrationFact]
    public async Task DeleteAsync_RemovesRole_AndItsJunctionRows()
    {
        var request = NewRequest(userIds: [UserA, UserB]);
        await _repository.InsertAsync(request);

        // FK_AppUserRole_AppRole has no ON DELETE CASCADE — deleting the role without clearing
        // the junction first would throw 547.
        var deleted = await _repository.DeleteAsync(request.RoleId);

        Assert.True(deleted);
        Assert.Null(await _repository.GetByIdAsync(request.RoleId));
        Assert.Empty(await ReadUserIdsAsync(request.RoleId));
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
        Assert.False(await _repository.ExistsAsync(request.RoleId));

        await _repository.InsertAsync(request);

        Assert.True(await _repository.ExistsAsync(request.RoleId));
    }
}
