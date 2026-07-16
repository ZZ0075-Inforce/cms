using CMS.API.Models;
using CMS.API.Repositories;
using CMS.API.Tests.Infrastructure;
using Dapper;

namespace CMS.API.Tests.Repositories;

/// <summary>
/// Runs real SQL against the real CMS database. Mocking Dapper would prove nothing — a broken ESCAPE
/// clause, a wrong tinyint cast, or a missing RowAudit row only fails against a real server.
///
/// PublishStatus is unusual: its pkid is a client-supplied tinyint (NOT an IDENTITY). Each test allocates
/// an unused pkid via <see cref="DatabaseFixture.NextPublishStatusPkidAsync"/> and a TEST_-prefixed
/// Description, then cleans up both the rows and the RowAudit rows it produced. Production rows
/// (草稿 / 已上架 / …) are never touched.
///
/// Run without a DB:  dotnet test --filter "Category!=Integration"
/// </summary>
[Trait("Category", "Integration")]
[Collection(DatabaseCollection.Name)]
public class PublishStatusRepositoryIntegrationTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private const string AuditTable = "PublishStatus";

    private readonly PublishStatusRepository _repository = new(fixture.ConnectionFactory, fixture.AuditWriter);
    private readonly List<byte> _created = [];

    public Task InitializeAsync() => Task.CompletedTask;

    /// <summary>
    /// Removes only what this test created — the RowAudit rows first, then the PublishStatus rows
    /// directly (not via the repository, so cleanup writes no further audit rows). Never touches
    /// production rows.
    /// </summary>
    public async Task DisposeAsync()
    {
        if (!DatabaseProbe.IsAvailable || _created.Count == 0) return;

        var keys = _created.Select(p => p.ToString()).ToArray();

        await using var conn = await fixture.OpenAsync();
        await conn.ExecuteAsync(
            "DELETE FROM dbo.RowAudit WHERE TableName = @T AND PrimaryKeyValues IN @Keys;",
            new { T = AuditTable, Keys = keys });
        await conn.ExecuteAsync(
            "DELETE FROM dbo.PublishStatus WHERE pkid IN @Keys;", new { Keys = _created });
    }

    /// <summary>Allocates an unused pkid + TEST_ Description, inserts, and registers it for cleanup.</summary>
    private async Task<PublishStatusRequest> InsertNewAsync(
        string? description = null,
        bool isDraft = false, bool isPublished = false, bool isDiscontinued = false)
    {
        var request = new PublishStatusRequest
        {
            Pkid = await fixture.NextPublishStatusPkidAsync(),
            Description = description ?? DatabaseFixture.NewPublishStatusDescription(),
            IsDraft = isDraft,
            IsPublished = isPublished,
            IsDiscontinued = isDiscontinued
        };

        var pkid = await _repository.InsertAsync(request);
        _created.Add(pkid);
        return request;
    }

    private async Task<List<RowAudit>> AuditRowsAsync(byte pkid)
    {
        await using var conn = await fixture.OpenAsync();
        var rows = await conn.QueryAsync<RowAudit>("""
            SELECT pkid AS Pkid, TableName, UserName, PrimaryKeyValues, ActionType, ActionDesc, [DateTime]
            FROM   dbo.RowAudit
            WHERE  TableName = @T AND PrimaryKeyValues = @Pkid
            ORDER  BY pkid;
            """, new { T = AuditTable, Pkid = pkid.ToString() });
        return rows.AsList();
    }

    // ---------- Insert ----------

    [IntegrationFact]
    public async Task InsertAsync_ReturnsTheSuppliedPkid_AndPersistsEveryColumn()
    {
        var request = await InsertNewAsync(isDraft: true, isPublished: false, isDiscontinued: false);

        var status = await _repository.GetByIdAsync(request.Pkid);

        Assert.NotNull(status);
        Assert.Equal(request.Pkid, status.Pkid);         // client-supplied, not generated
        Assert.Equal(request.Description, status.Description);   // nvarchar round-trip
        Assert.True(status.IsDraft);
        Assert.False(status.IsPublished);
        Assert.False(status.IsDiscontinued);
    }

    [IntegrationFact]
    public async Task InsertAsync_WritesInsertAuditRow_WithDescriptionAsActionDesc()
    {
        var request = await InsertNewAsync();

        var audit = Assert.Single(await AuditRowsAsync(request.Pkid));
        Assert.Equal("Insert", audit.ActionType);
        Assert.Equal(request.Description, audit.ActionDesc);   // Description is the first string property
        Assert.Equal(request.Pkid.ToString(), audit.PrimaryKeyValues);
        Assert.Equal("system", audit.UserName);               // no HttpContext in scope
    }

    // ---------- Exists ----------

    [IntegrationFact]
    public async Task ExistsAsync_TrueForAnInsertedRow_FalseOtherwise()
    {
        var request = await InsertNewAsync();

        Assert.True(await _repository.ExistsAsync(request.Pkid));
        Assert.False(await _repository.ExistsAsync(await fixture.NextPublishStatusPkidAsync()));
    }

    // ---------- GetById ----------

    [IntegrationFact]
    public async Task GetByIdAsync_ReturnsNull_WhenMissing()
    {
        var unused = await fixture.NextPublishStatusPkidAsync();

        Assert.Null(await _repository.GetByIdAsync(unused));
    }

    [IntegrationFact]
    public async Task GetByIdAsync_ProjectsCourseCount_ForAStatusWithNoCourses()
    {
        var request = await InsertNewAsync();

        var status = await _repository.GetByIdAsync(request.Pkid);

        Assert.NotNull(status);
        Assert.Equal(0, status.CourseCount);
    }

    // ---------- GetAll ----------

    [IntegrationFact]
    public async Task GetAllAsync_ContainsTheNewRow_OrderedByPkidAscending()
    {
        var request = await InsertNewAsync();

        var statuses = await _repository.GetAllAsync();

        // Containment, never an exact count — the table holds real production rows.
        Assert.Contains(statuses, s => s.Pkid == request.Pkid);

        var pkids = statuses.Select(s => (int)s.Pkid).ToList();
        Assert.Equal(pkids.OrderBy(x => x), pkids);
    }

    // ---------- Query ----------

    [IntegrationFact]
    public async Task QueryAsync_Keyword_MatchesDescription()
    {
        var request = await InsertNewAsync();

        var results = await _repository.QueryAsync(new PublishStatusQuery { Keyword = request.Description });

        Assert.Equal(request.Pkid, Assert.Single(results).Pkid);
    }

    [IntegrationFact]
    public async Task QueryAsync_Keyword_EscapesLikeWildcards()
    {
        var request = await InsertNewAsync();

        // "%" must be a literal, not a wildcard. Unescaped, this predicate degrades to "match every row"
        // — the precise bug ESCAPE '\' guards against.
        var results = await _repository.QueryAsync(new PublishStatusQuery { Keyword = "%" });

        Assert.DoesNotContain(results, s => s.Pkid == request.Pkid);
        Assert.Empty(results);
    }

    [IntegrationFact]
    public async Task QueryAsync_BitFilter_IsTriState()
    {
        // A published-only row: IsPublished true, the rest false.
        var request = await InsertNewAsync(isPublished: true);

        var published = await _repository.QueryAsync(
            new PublishStatusQuery { Keyword = request.Description, IsPublished = true });
        var notPublished = await _repository.QueryAsync(
            new PublishStatusQuery { Keyword = request.Description, IsPublished = false });
        var notDraft = await _repository.QueryAsync(
            new PublishStatusQuery { Keyword = request.Description, IsDraft = false });

        Assert.Contains(published, s => s.Pkid == request.Pkid);        // matches the true filter
        Assert.DoesNotContain(notPublished, s => s.Pkid == request.Pkid); // excluded by the false filter
        Assert.Contains(notDraft, s => s.Pkid == request.Pkid);          // IsDraft is false → matches
    }

    [IntegrationFact]
    public async Task QueryAsync_NoFilters_ReturnsEverything()
    {
        var request = await InsertNewAsync();

        var results = await _repository.QueryAsync(new PublishStatusQuery());

        Assert.Contains(results, s => s.Pkid == request.Pkid);
    }

    // ---------- Update ----------

    [IntegrationFact]
    public async Task UpdateAsync_ChangesWritableColumns_AndKeepsThePkid()
    {
        var request = await InsertNewAsync(isPublished: false);

        request.Description = DatabaseFixture.NewPublishStatusDescription();
        request.IsPublished = true;
        var updated = await _repository.UpdateAsync(request);

        Assert.True(updated);
        var status = await _repository.GetByIdAsync(request.Pkid);
        Assert.NotNull(status);
        Assert.Equal(request.Description, status.Description);
        Assert.True(status.IsPublished);
        Assert.Equal(request.Pkid, status.Pkid);   // the key must survive an update
    }

    [IntegrationFact]
    public async Task UpdateAsync_WritesUpdateAuditRow_ListingExactlyTheChangedColumns()
    {
        var request = await InsertNewAsync(isPublished: false);

        // Change only Description and IsPublished; leave IsDraft / IsDiscontinued untouched.
        request.Description = DatabaseFixture.NewPublishStatusDescription();
        request.IsPublished = true;
        Assert.True(await _repository.UpdateAsync(request));

        var updates = (await AuditRowsAsync(request.Pkid)).Where(a => a.ActionType == "Update").ToList();
        var update = Assert.Single(updates);
        Assert.Equal("Description,IsPublished", update.ActionDesc);   // exactly the two changed, in order
        Assert.Equal("system", update.UserName);
    }

    [IntegrationFact]
    public async Task UpdateAsync_ReturnsFalse_WhenPkidMissing()
    {
        var request = new PublishStatusRequest
        {
            Pkid = await fixture.NextPublishStatusPkidAsync(),   // unused → nothing to update
            Description = DatabaseFixture.NewPublishStatusDescription()
        };

        Assert.False(await _repository.UpdateAsync(request));
    }

    // ---------- Delete ----------

    [IntegrationFact]
    public async Task DeleteAsync_RemovesTheStatus_AndWritesADeleteAuditRow()
    {
        var request = await InsertNewAsync();

        var deleted = await _repository.DeleteAsync(request.Pkid);

        Assert.True(deleted);
        Assert.Null(await _repository.GetByIdAsync(request.Pkid));

        var deletes = (await AuditRowsAsync(request.Pkid)).Where(a => a.ActionType == "Delete").ToList();
        var delete = Assert.Single(deletes);
        Assert.Equal(request.Description, delete.ActionDesc);
        Assert.Equal(request.Pkid.ToString(), delete.PrimaryKeyValues);
    }

    [IntegrationFact]
    public async Task DeleteAsync_ReturnsFalse_WhenMissing()
    {
        var unused = await fixture.NextPublishStatusPkidAsync();

        Assert.False(await _repository.DeleteAsync(unused));
    }
}
