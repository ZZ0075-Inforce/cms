using CMS.API.Models;
using CMS.API.Repositories;
using CMS.API.Tests.Infrastructure;
using Dapper;

namespace CMS.API.Tests.Repositories;

/// <summary>
/// Runs real SQL against the real CMS database. The whole point of Course's integration coverage is
/// the parts a mock cannot catch: the 3-way JOIN labels, the DateOnly round-trip, and the two
/// transaction-wrapped N-N syncs (a wrong junction column or broken transaction only fails for real).
///
/// It seeds its own TEST_ Partner / Certification / JobCategory as valid FK targets and borrows one
/// existing PublishStatus pkid (that table is a fixed enum — read-only, never mutated).
///
/// Run without a DB:  dotnet test --filter "Category!=Integration"
/// </summary>
[Trait("Category", "Integration")]
[Collection(DatabaseCollection.Name)]
public class CourseRepositoryIntegrationTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private readonly CourseRepository _repository = new(fixture.ConnectionFactory, fixture.AuditWriter);
    private readonly PartnerRepository _partners = new(fixture.ConnectionFactory, fixture.AuditWriter);
    private readonly List<int> _createdCourses = [];

    private short _partnerPkid;
    private byte _publishStatusPkid;
    private int _certPkid;
    private short _jobCatPkid;

    public async Task InitializeAsync()
    {
        if (!DatabaseProbe.IsAvailable) return;

        // A real Partner for the required FK.
        _partnerPkid = await _partners.InsertAsync(new PartnerRequest
        {
            Name = DatabaseFixture.NewPartnerName(),
            AppKey = DatabaseFixture.NewAppKey(),
            NameOnPartnerMenu = "測試選單",
            NameOnCourseDetailPage = "測試",
            DisplayOrder = 500
        });

        _publishStatusPkid = await fixture.AnyPublishStatusPkidAsync();

        // Real Certification + JobCategory rows for the two N-N sets.
        await using var conn = await fixture.OpenAsync();
        _certPkid = await conn.ExecuteScalarAsync<int>("""
            INSERT INTO dbo.Certification (Partner_pkid, Title) VALUES (@Partner, @Title);
            SELECT CAST(SCOPE_IDENTITY() AS int);
            """, new { Partner = _partnerPkid, Title = DatabaseFixture.NewCourseId() });
        _jobCatPkid = await conn.ExecuteScalarAsync<short>("""
            INSERT INTO dbo.JobCategory (Description) VALUES (@Desc);
            SELECT CAST(SCOPE_IDENTITY() AS smallint);
            """, new { Desc = DatabaseFixture.NewCourseGroupName() });
    }

    /// <summary>Removes only what this test created, in FK order. Never touches production data.</summary>
    public async Task DisposeAsync()
    {
        if (!DatabaseProbe.IsAvailable) return;

        // Courses first: their cascade clears the CourseInCertification / CourseJobCategories rows
        // that would otherwise pin the Certification / JobCategory below.
        foreach (var pkid in _createdCourses)
            await _repository.DeleteAsync(pkid);

        await using var conn = await fixture.OpenAsync();
        await conn.ExecuteAsync("DELETE FROM dbo.Certification WHERE pkid = @Pkid;", new { Pkid = _certPkid });
        await conn.ExecuteAsync("DELETE FROM dbo.JobCategory WHERE pkid = @Pkid;", new { Pkid = _jobCatPkid });

        // Partner last — Certification FK'd it.
        if (_partnerPkid > 0)
            await _partners.DeleteAsync(_partnerPkid);
    }

    private CourseRequest NewRequest(
        IEnumerable<int>? certs = null, IEnumerable<short>? jobCats = null,
        short? courseGroupPkid = null, int displayOrder = 500) => new()
    {
        Title = "測試課程",
        OfficialTitle = "官方名稱",
        CourseId = DatabaseFixture.NewCourseId(),
        ProdCourseId = DatabaseFixture.NewCourseId(),
        FriendlyUrl = DatabaseFixture.NewCourseId(),
        DisplayOrder = displayOrder,
        PartnerPkid = _partnerPkid,
        CourseGroupPkid = courseGroupPkid,
        PublishStatusPkid = _publishStatusPkid,
        ScheduleOn = new DateOnly(2026, 1, 1),
        ScheduleOff = new DateOnly(2036, 1, 1),
        Hour = 21,
        ListPrice = 15000m,
        LearningCredit = 3.5m,
        CanRepeat = true,
        CertificationPkids = certs?.ToList() ?? [],
        JobCategoryPkids = jobCats?.ToList() ?? []
    };

    private async Task<int> InsertAsync(CourseRequest request)
    {
        var pkid = await _repository.InsertAsync(request);
        _createdCourses.Add(pkid);
        request.Pkid = pkid;
        return pkid;
    }

    // ---------- Insert + GetById ----------

    [IntegrationFact]
    public async Task InsertAsync_PersistsScalarsDatesAndBothNnSets()
    {
        var pkid = await InsertAsync(NewRequest(certs: [_certPkid], jobCats: [_jobCatPkid]));

        var course = await _repository.GetByIdAsync(pkid);

        Assert.NotNull(course);
        Assert.Equal("測試課程", course.Title);
        Assert.Equal(new DateOnly(2026, 1, 1), course.ScheduleOn);   // DateOnly round-trip
        Assert.Equal(3.5m, course.LearningCredit);
        Assert.True(course.CanRepeat);
        Assert.Contains(_certPkid, course.CertificationPkids);
        Assert.Contains(_jobCatPkid, course.JobCategoryPkids);
    }

    [IntegrationFact]
    public async Task GetByIdAsync_ResolvesTheJoinLabels()
    {
        var pkid = await InsertAsync(NewRequest());

        var course = await _repository.GetByIdAsync(pkid);

        Assert.NotNull(course);
        // PartnerName comes from the INNER JOIN; CourseGroupName is null (LEFT JOIN, no group).
        Assert.False(string.IsNullOrEmpty(course.PartnerName));
        Assert.Null(course.CourseGroupPkid);
        Assert.Null(course.CourseGroupName);
        Assert.False(string.IsNullOrEmpty(course.PublishStatusName));
    }

    [IntegrationFact]
    public async Task GetByIdAsync_ReturnsNull_WhenMissing()
    {
        Assert.Null(await _repository.GetByIdAsync(-1));
    }

    // ---------- Update re-syncs the N-N ----------

    [IntegrationFact]
    public async Task UpdateAsync_ReplacesTheCertificationSet()
    {
        var request = NewRequest(certs: [_certPkid]);
        var pkid = await InsertAsync(request);

        // Drop the certification; the delete-then-reinsert must leave the set empty.
        request.CertificationPkids = [];
        var updated = await _repository.UpdateAsync(request);

        Assert.True(updated);
        var course = await _repository.GetByIdAsync(pkid);
        Assert.NotNull(course);
        Assert.Empty(course.CertificationPkids);
    }

    [IntegrationFact]
    public async Task UpdateAsync_DeduplicatesJobCategoryPayload()
    {
        var request = NewRequest();
        var pkid = await InsertAsync(request);

        // A duplicate in the payload must not throw 2627 — the sync .Distinct()s first.
        request.JobCategoryPkids = [_jobCatPkid, _jobCatPkid];
        var updated = await _repository.UpdateAsync(request);

        Assert.True(updated);
        var course = await _repository.GetByIdAsync(pkid);
        Assert.NotNull(course);
        Assert.Single(course.JobCategoryPkids);
    }

    [IntegrationFact]
    public async Task UpdateAsync_ReturnsFalse_WhenPkidMissing()
    {
        var request = NewRequest();
        request.Pkid = -1;

        Assert.False(await _repository.UpdateAsync(request));
    }

    // ---------- Query ----------

    [IntegrationFact]
    public async Task QueryAsync_FiltersByPartner_AndContainsTheNewRow()
    {
        var pkid = await InsertAsync(NewRequest());

        var results = await _repository.QueryAsync(new CourseQuery { PartnerPkid = _partnerPkid });

        // Containment, never an exact count — the table holds real production rows.
        Assert.Contains(results, c => c.Pkid == pkid);
        Assert.All(results, c => Assert.Equal(_partnerPkid, c.PartnerPkid));
    }

    [IntegrationFact]
    public async Task QueryAsync_Keyword_MatchesCourseId()
    {
        var request = NewRequest();
        var pkid = await InsertAsync(request);

        var results = await _repository.QueryAsync(new CourseQuery { Keyword = request.CourseId });

        Assert.Equal(pkid, Assert.Single(results).Pkid);
    }

    [IntegrationFact]
    public async Task QueryAsync_Keyword_EscapesLikeWildcards()
    {
        var pkid = await InsertAsync(NewRequest());

        var results = await _repository.QueryAsync(new CourseQuery { Keyword = "%" });

        Assert.DoesNotContain(results, c => c.Pkid == pkid);
        Assert.Empty(results);
    }

    [IntegrationFact]
    public async Task QueryAsync_ScheduleOnRange_ExcludesOutOfRange()
    {
        var pkid = await InsertAsync(NewRequest());   // ScheduleOn = 2026-01-01

        var inRange = await _repository.QueryAsync(new CourseQuery
        {
            PartnerPkid = _partnerPkid,
            ScheduleOnFrom = new DateOnly(2025, 12, 1),
            ScheduleOnTo = new DateOnly(2026, 2, 1)
        });
        var outOfRange = await _repository.QueryAsync(new CourseQuery
        {
            PartnerPkid = _partnerPkid,
            ScheduleOnFrom = new DateOnly(2030, 1, 1)
        });

        Assert.Contains(inRange, c => c.Pkid == pkid);
        Assert.DoesNotContain(outOfRange, c => c.Pkid == pkid);
    }

    // ---------- Delete ----------

    [IntegrationFact]
    public async Task DeleteAsync_RemovesTheCourse_AndCascadesItsNnRows()
    {
        var pkid = await InsertAsync(NewRequest(certs: [_certPkid], jobCats: [_jobCatPkid]));

        var deleted = await _repository.DeleteAsync(pkid);

        Assert.True(deleted);
        Assert.Null(await _repository.GetByIdAsync(pkid));

        // The junction rows cascaded away, so the Certification/JobCategory are now free to delete
        // (verified implicitly by DisposeAsync not throwing 547).
    }

    [IntegrationFact]
    public async Task DeleteAsync_ReturnsFalse_WhenMissing()
    {
        Assert.False(await _repository.DeleteAsync(-1));
    }
}
