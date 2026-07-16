using CMS.API.Models;
using CMS.API.Repositories;
using CMS.API.Tests.Infrastructure;

namespace CMS.API.Tests.Repositories;

/// <summary>
/// Runs real SQL against the real CMS database. Mocking Dapper would prove nothing — a broken
/// ESCAPE clause or a wrong CAST on SCOPE_IDENTITY only fails against a real server.
///
/// Run without a DB:  dotnet test --filter "Category!=Integration"
/// </summary>
[Trait("Category", "Integration")]
[Collection(DatabaseCollection.Name)]
public class CourseGroupRepositoryIntegrationTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private readonly CourseGroupRepository _repository = new(fixture.ConnectionFactory, fixture.AuditWriter);
    private readonly List<short> _created = [];

    public Task InitializeAsync() => Task.CompletedTask;

    /// <summary>
    /// Removes only what this test created. These groups are seeded with no courses, so the
    /// FK_Course_CourseGroup cascade has nothing to take with it. Never touches production rows.
    /// </summary>
    public async Task DisposeAsync()
    {
        if (!DatabaseProbe.IsAvailable) return;

        foreach (var pkid in _created)
            await _repository.DeleteAsync(pkid);
    }

    /// <summary>Builds a request with a unique, TEST_-prefixed Description.</summary>
    private static CourseGroupRequest NewRequest(string? description = null) => new()
    {
        Description = description ?? DatabaseFixture.NewCourseGroupName()
    };

    /// <summary>Inserts and registers the row for cleanup, returning the DB-generated pkid.</summary>
    private async Task<short> InsertAsync(CourseGroupRequest request)
    {
        var pkid = await _repository.InsertAsync(request);
        _created.Add(pkid);
        request.Pkid = pkid;
        return pkid;
    }

    // ---------- Insert ----------

    [IntegrationFact]
    public async Task InsertAsync_ReturnsIdentityPkid_AndPersistsDescription()
    {
        var request = NewRequest();

        var pkid = await InsertAsync(request);

        Assert.True(pkid > 0);
        var group = await _repository.GetByIdAsync(pkid);
        Assert.NotNull(group);
        Assert.Equal(request.Description, group.Description);   // nvarchar round-trip
    }

    // ---------- GetById ----------

    [IntegrationFact]
    public async Task GetByIdAsync_ReturnsNull_WhenMissing()
    {
        Assert.Null(await _repository.GetByIdAsync(-1));
    }

    [IntegrationFact]
    public async Task GetByIdAsync_ProjectsCourseCount_ForAGroupWithNoCourses()
    {
        var pkid = await InsertAsync(NewRequest());

        var group = await _repository.GetByIdAsync(pkid);

        Assert.NotNull(group);
        Assert.Equal(0, group.CourseCount);
    }

    // ---------- GetAll ----------

    [IntegrationFact]
    public async Task GetAllAsync_ContainsTheNewRow_OrderedByPkidDescending()
    {
        var first = await InsertAsync(NewRequest());
        var second = await InsertAsync(NewRequest());

        var groups = await _repository.GetAllAsync();

        // Containment, never an exact count — the table holds real production rows.
        Assert.Contains(groups, g => g.Pkid == first);
        Assert.Contains(groups, g => g.Pkid == second);

        var pkids = groups.Select(g => (int)g.Pkid).ToList();
        Assert.Equal(pkids.OrderByDescending(x => x), pkids);
    }

    // ---------- Query ----------

    [IntegrationFact]
    public async Task QueryAsync_Keyword_MatchesDescription()
    {
        var request = NewRequest();
        var pkid = await InsertAsync(request);

        var results = await _repository.QueryAsync(new CourseGroupQuery { Keyword = request.Description });

        Assert.Equal(pkid, Assert.Single(results).Pkid);
    }

    [IntegrationFact]
    public async Task QueryAsync_Keyword_EscapesLikeWildcards()
    {
        var pkid = await InsertAsync(NewRequest());

        // "%" must be a literal, not a wildcard. Unescaped, this predicate degrades to
        // "match every row" — the precise bug ESCAPE '\' guards against.
        var results = await _repository.QueryAsync(new CourseGroupQuery { Keyword = "%" });

        Assert.DoesNotContain(results, g => g.Pkid == pkid);
        Assert.Empty(results);
    }

    [IntegrationFact]
    public async Task QueryAsync_NoFilters_ReturnsEverything()
    {
        var pkid = await InsertAsync(NewRequest());

        var results = await _repository.QueryAsync(new CourseGroupQuery());

        Assert.Contains(results, g => g.Pkid == pkid);
    }

    // ---------- Update ----------

    [IntegrationFact]
    public async Task UpdateAsync_ChangesTheDescription_AndKeepsThePkid()
    {
        var request = NewRequest();
        var pkid = await InsertAsync(request);

        request.Description = DatabaseFixture.NewCourseGroupName();
        var updated = await _repository.UpdateAsync(request);

        Assert.True(updated);
        var group = await _repository.GetByIdAsync(pkid);
        Assert.NotNull(group);
        Assert.Equal(request.Description, group.Description);
        Assert.Equal(pkid, group.Pkid);   // the IDENTITY must survive an update
    }

    [IntegrationFact]
    public async Task UpdateAsync_ReturnsFalse_WhenPkidMissing()
    {
        var request = NewRequest();
        request.Pkid = -1;

        Assert.False(await _repository.UpdateAsync(request));
    }

    // ---------- Delete ----------

    [IntegrationFact]
    public async Task DeleteAsync_RemovesTheGroup()
    {
        var pkid = await InsertAsync(NewRequest());

        var deleted = await _repository.DeleteAsync(pkid);

        Assert.True(deleted);
        Assert.Null(await _repository.GetByIdAsync(pkid));
    }

    [IntegrationFact]
    public async Task DeleteAsync_ReturnsFalse_WhenMissing()
    {
        Assert.False(await _repository.DeleteAsync(-1));
    }
}
