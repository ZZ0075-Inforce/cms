using CMS.API.Models;
using CMS.API.Repositories;
using CMS.API.Tests.Infrastructure;
using Dapper;

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

    [IntegrationFact]
    public async Task DeleteAsync_AuditsEveryCourseTheCascadeTakesWithIt()
    {
        // The only test that puts a Course inside the group before deleting it. FK_Course_CourseGroup
        // is ON DELETE CASCADE, so the DB silently removes the course too — and SQL Server writes no
        // audit of its own. Without this, N courses can vanish leaving a trail naming only the group.
        var partners = new PartnerRepository(fixture.ConnectionFactory, fixture.AuditWriter);
        var courses = new CourseRepository(fixture.ConnectionFactory, fixture.AuditWriter);

        var partnerPkid = await partners.InsertAsync(new PartnerRequest
        {
            Name = DatabaseFixture.NewPartnerName(),
            AppKey = DatabaseFixture.NewAppKey(),
            NameOnPartnerMenu = "測試選單",
            NameOnCourseDetailPage = "測試",
            DisplayOrder = 500
        });

        try
        {
            var groupPkid = await _repository.InsertAsync(NewRequest()); // NOT registered for cleanup:
                                                                        // this test deletes it itself.
            var courseTitle = $"{DatabaseFixture.Prefix}cascade-{Guid.NewGuid():N}"[..24];
            var coursePkid = await courses.InsertAsync(new CourseRequest
            {
                Title = courseTitle,
                CourseId = DatabaseFixture.NewCourseId(),
                ProdCourseId = DatabaseFixture.NewCourseId(),
                FriendlyUrl = DatabaseFixture.NewCourseId(),
                DisplayOrder = 500,
                PartnerPkid = partnerPkid,
                CourseGroupPkid = groupPkid,
                PublishStatusPkid = await fixture.AnyPublishStatusPkidAsync(),
                ScheduleOn = new DateOnly(2026, 1, 1),
                ScheduleOff = new DateOnly(2036, 1, 1),
                Hour = 21,
                ListPrice = 15000m,
                LearningCredit = 3.5m
            });

            var deleted = await _repository.DeleteAsync(groupPkid);

            Assert.True(deleted);
            // The cascade really did fire — the course is gone.
            Assert.Null(await courses.GetByIdAsync(coursePkid));

            // ...and the trail records it as a Course delete in its own right, keyed on the course's
            // own pkid, not merely as a side note on the group.
            await using var conn = await fixture.OpenAsync();
            var auditedCourseDeletes = await conn.QueryAsync<string>(
                """
                SELECT ActionDesc FROM dbo.RowAudit
                WHERE TableName = 'Course' AND ActionType = 'Delete' AND PrimaryKeyValues = @Pkid;
                """,
                new { Pkid = coursePkid.ToString() });

            Assert.Contains(courseTitle, auditedCourseDeletes);

            // The group's own delete is still audited too — the child rows are additional, not a swap.
            var groupDeletes = await conn.ExecuteScalarAsync<int>(
                """
                SELECT COUNT(*) FROM dbo.RowAudit
                WHERE TableName = 'CourseGroup' AND ActionType = 'Delete' AND PrimaryKeyValues = @Pkid;
                """,
                new { Pkid = groupPkid.ToString() });
            Assert.Equal(1, groupDeletes);
        }
        finally
        {
            await partners.DeleteAsync(partnerPkid);
        }
    }
}
