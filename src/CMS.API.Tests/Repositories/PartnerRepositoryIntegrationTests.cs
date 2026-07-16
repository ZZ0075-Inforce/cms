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
public class PartnerRepositoryIntegrationTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private readonly PartnerRepository _repository = new(fixture.ConnectionFactory, fixture.AuditWriter);
    private readonly List<short> _created = [];

    public Task InitializeAsync() => Task.CompletedTask;

    /// <summary>Removes only what this test created. Never touches production partners.</summary>
    public async Task DisposeAsync()
    {
        if (!DatabaseProbe.IsAvailable) return;

        foreach (var pkid in _created)
            await _repository.DeleteAsync(pkid);
    }

    /// <summary>Builds a request with a unique, TEST_-prefixed Name.</summary>
    private static PartnerRequest NewRequest(
        string? name = null, string? menuName = null, string? detailName = null,
        int displayOrder = 500, string? imageFilename = null) => new()
    {
        Name = name ?? DatabaseFixture.NewPartnerName(),
        AppKey = DatabaseFixture.NewAppKey(),
        NameOnPartnerMenu = menuName ?? "選單名稱",
        NameOnCourseDetailPage = detailName ?? "明細頁名稱",
        DisplayOrder = displayOrder,
        ImageFilename = imageFilename
    };

    /// <summary>Inserts and registers the row for cleanup, returning the DB-generated pkid.</summary>
    private async Task<short> InsertAsync(PartnerRequest request)
    {
        var pkid = await _repository.InsertAsync(request);
        _created.Add(pkid);
        request.Pkid = pkid;
        return pkid;
    }

    // ---------- Insert ----------

    [IntegrationFact]
    public async Task InsertAsync_ReturnsIdentityPkid_AndPersistsEveryColumn()
    {
        var request = NewRequest(menuName: "微軟選單", detailName: "微軟", imageFilename: "ms.png");

        var pkid = await InsertAsync(request);

        Assert.True(pkid > 0);
        var partner = await _repository.GetByIdAsync(pkid);
        Assert.NotNull(partner);
        Assert.Equal(request.Name, partner.Name);
        Assert.Equal(request.AppKey, partner.AppKey);
        Assert.Equal("微軟選單", partner.NameOnPartnerMenu);   // nvarchar round-trip
        Assert.Equal("微軟", partner.NameOnCourseDetailPage);
        Assert.Equal(500, partner.DisplayOrder);
        Assert.Equal("ms.png", partner.ImageFilename);
    }

    [IntegrationFact]
    public async Task InsertAsync_NormalisesBlankImageFilename_ToNull()
    {
        // A blank from the form must land as NULL, not as an empty string.
        var pkid = await InsertAsync(NewRequest(imageFilename: "   "));

        var partner = await _repository.GetByIdAsync(pkid);

        Assert.NotNull(partner);
        Assert.Null(partner.ImageFilename);
    }

    // ---------- GetById ----------

    [IntegrationFact]
    public async Task GetByIdAsync_ReturnsNull_WhenMissing()
    {
        Assert.Null(await _repository.GetByIdAsync(-1));
    }

    [IntegrationFact]
    public async Task GetByIdAsync_ProjectsCourseCount_ForAPartnerWithNoCourses()
    {
        var pkid = await InsertAsync(NewRequest());

        var partner = await _repository.GetByIdAsync(pkid);

        Assert.NotNull(partner);
        Assert.Equal(0, partner.CourseCount);
    }

    // ---------- GetAll ----------

    [IntegrationFact]
    public async Task GetAllAsync_ContainsTheNewRow_OrderedByDisplayOrder()
    {
        var pkid = await InsertAsync(NewRequest());

        var partners = await _repository.GetAllAsync();

        // Containment, never an exact count — the table holds real production rows.
        Assert.Contains(partners, p => p.Pkid == pkid);

        var orders = partners.Select(p => p.DisplayOrder).ToList();
        Assert.Equal(orders.OrderBy(x => x), orders);
    }

    // ---------- Query ----------

    [IntegrationTheory]
    [InlineData("ZZmarkerMenu")]     // hits NameOnPartnerMenu
    [InlineData("ZZmarkerDetail")]   // hits NameOnCourseDetailPage
    public async Task QueryAsync_Keyword_MatchesTheDisplayNameColumns(string keyword)
    {
        var request = NewRequest(
            menuName: "a ZZmarkerMenu here", detailName: "a ZZmarkerDetail here");
        var pkid = await InsertAsync(request);

        var results = await _repository.QueryAsync(new PartnerQuery { Keyword = keyword });

        Assert.Equal(pkid, Assert.Single(results).Pkid);
    }

    [IntegrationFact]
    public async Task QueryAsync_Keyword_MatchesNameAndAppKey()
    {
        var request = NewRequest();
        var pkid = await InsertAsync(request);

        var byName = await _repository.QueryAsync(new PartnerQuery { Keyword = request.Name });
        var byAppKey = await _repository.QueryAsync(new PartnerQuery { Keyword = request.AppKey });

        Assert.Equal(pkid, Assert.Single(byName).Pkid);
        Assert.Contains(byAppKey, p => p.Pkid == pkid);
    }

    [IntegrationFact]
    public async Task QueryAsync_Keyword_EscapesLikeWildcards()
    {
        var pkid = await InsertAsync(NewRequest(menuName: "no wildcards", detailName: "none either"));

        // "%" must be a literal, not a wildcard. Unescaped, this predicate degrades to
        // "match every row" — the precise bug ESCAPE '\' guards against.
        var results = await _repository.QueryAsync(new PartnerQuery { Keyword = "%" });

        Assert.DoesNotContain(results, p => p.Pkid == pkid);
        Assert.Empty(results);
    }

    [IntegrationFact]
    public async Task QueryAsync_NoFilters_ReturnsEverything()
    {
        var pkid = await InsertAsync(NewRequest());

        var results = await _repository.QueryAsync(new PartnerQuery());

        Assert.Contains(results, p => p.Pkid == pkid);
    }

    // ---------- Update ----------

    [IntegrationFact]
    public async Task UpdateAsync_UpdatesEveryWritableColumn_IncludingAppKey()
    {
        var request = NewRequest(menuName: "Before", detailName: "Before", displayOrder: 500);
        var pkid = await InsertAsync(request);

        // AppKey is editable here — it is not a key and nothing FKs to it.
        request.AppKey = DatabaseFixture.NewAppKey();
        request.NameOnPartnerMenu = "After";
        request.NameOnCourseDetailPage = "After";
        request.DisplayOrder = 501;
        request.ImageFilename = null;
        var updated = await _repository.UpdateAsync(request);

        Assert.True(updated);
        var partner = await _repository.GetByIdAsync(pkid);
        Assert.NotNull(partner);
        Assert.Equal(request.AppKey, partner.AppKey);
        Assert.Equal("After", partner.NameOnPartnerMenu);
        Assert.Equal(501, partner.DisplayOrder);
        Assert.Null(partner.ImageFilename);
        Assert.Equal(pkid, partner.Pkid);   // the IDENTITY must survive an update
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
    public async Task DeleteAsync_RemovesThePartner()
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
