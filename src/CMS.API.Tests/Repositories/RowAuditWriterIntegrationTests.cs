using CMS.API.Models;
using CMS.API.Repositories;
using CMS.API.Tests.Infrastructure;
using Dapper;
using Microsoft.Data.SqlClient;

namespace CMS.API.Tests.Repositories;

/// <summary>
/// Proves the RowAuditWriter retrofit end-to-end against the real database, through one representative
/// repository (FeaturedPromoItem): each successful Insert / Update / Delete writes exactly the right
/// RowAudit row, and a change that fails (a duplicate that trips the UNIQUE index) rolls back with the
/// audit row too — so no orphan audit entry survives a failed change.
///
/// The writer here has no HttpContext, so every row is attributed to "system" (see
/// <see cref="DatabaseFixture.AuditWriter"/>). It seeds its own TEST_ Promotion2 for the FK and cleans
/// up both the FeaturedPromoItem rows and the RowAudit rows it produced.
///
/// Run without a DB:  dotnet test --filter "Category!=Integration"
/// </summary>
[Trait("Category", "Integration")]
[Collection(DatabaseCollection.Name)]
public class RowAuditWriterIntegrationTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private const string AuditTable = "FeaturedPromoItem";

    private readonly FeaturedPromoItemRepository _repository = new(fixture.ConnectionFactory, fixture.AuditWriter);
    private readonly List<int> _created = [];

    private int _promotionPkid;
    private short _centerPkid;

    private const string PromoTopic = "TEST_稽核測試主題";
    private const string PromoDescription = "稽核測試說明";

    public async Task InitializeAsync()
    {
        if (!DatabaseProbe.IsAvailable) return;

        var publishStatusPkid = await fixture.AnyPublishStatusPkidAsync();
        _centerPkid = await fixture.AnyTrainingCenterPkidAsync();

        await using var conn = await fixture.OpenAsync();
        _promotionPkid = await conn.ExecuteScalarAsync<int>("""
            INSERT INTO dbo.Promotion2
                (PublishStatus_pkid, ObjectId, PromoCode, Topic, Description, DisplayOrder, ScheduleOn, ScheduleOff)
            VALUES
                (@Ps, NEWID(), @Code, @Topic, @Desc, 0, @On, @Off);
            SELECT CAST(SCOPE_IDENTITY() AS int);
            """, new
        {
            Ps = publishStatusPkid,
            Code = DatabaseFixture.NewPromoCode(),
            Topic = PromoTopic,
            Desc = PromoDescription,
            On = new DateOnly(2099, 1, 1),
            Off = new DateOnly(2099, 12, 31)
        });
    }

    public async Task DisposeAsync()
    {
        if (!DatabaseProbe.IsAvailable) return;

        await using var conn = await fixture.OpenAsync();

        // Delete directly (not via the repository) so cleanup does not itself write audit rows. Remove
        // the audit rows this test created, then the FeaturedPromoItem rows, then the seeded Promotion2.
        if (_created.Count > 0)
        {
            var keys = _created.Select(p => p.ToString()).ToArray();
            await conn.ExecuteAsync(
                "DELETE FROM dbo.RowAudit WHERE TableName = @T AND PrimaryKeyValues IN @Keys;",
                new { T = AuditTable, Keys = keys });
            await conn.ExecuteAsync(
                "DELETE FROM dbo.FeaturedPromoItem WHERE pkid IN @Keys;", new { Keys = _created });
        }

        await conn.ExecuteAsync("DELETE FROM dbo.Promotion2 WHERE pkid = @Pkid;", new { Pkid = _promotionPkid });
    }

    private FeaturedPromoItemRequest NewRequest(DateOnly scheduleOn, byte slot) => new()
    {
        ScheduleOn = scheduleOn,
        TrainingCenterPkid = _centerPkid,
        Slot = slot,
        PromotionPkid = _promotionPkid,
        Topic = PromoTopic,
        Description = PromoDescription
    };

    private async Task<int> InsertAsync(FeaturedPromoItemRequest request)
    {
        var pkid = await _repository.InsertAsync(request);
        _created.Add(pkid);
        request.Pkid = pkid;
        return pkid;
    }

    private async Task<List<RowAudit>> AuditRowsAsync(int pkid)
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

    private async Task<int> TotalAuditRowsAsync()
    {
        await using var conn = await fixture.OpenAsync();
        return await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM dbo.RowAudit WHERE TableName = @T;", new { T = AuditTable });
    }

    // ---------- Insert → one Insert row, ActionDesc = first string column (Topic) ----------

    [IntegrationFact]
    public async Task InsertAsync_WritesInsertAuditRow_WithFirstStringColumnAsActionDesc()
    {
        var pkid = await InsertAsync(NewRequest(new DateOnly(2099, 8, 3), slot: 1));

        var audit = Assert.Single(await AuditRowsAsync(pkid));
        Assert.Equal("Insert", audit.ActionType);
        Assert.Equal(PromoTopic, audit.ActionDesc);          // Topic is the first string property
        Assert.Equal(pkid.ToString(), audit.PrimaryKeyValues);
        Assert.Equal("system", audit.UserName);              // no HttpContext in scope
    }

    // ---------- Update → one Update row listing EXACTLY the changed columns ----------

    [IntegrationFact]
    public async Task UpdateAsync_WritesUpdateAuditRow_ListingExactlyTheChangedColumns()
    {
        var request = NewRequest(new DateOnly(2099, 8, 10), slot: 1);
        var pkid = await InsertAsync(request);

        // Change only Topic and Description; leave ScheduleOn / center / slot / promotion untouched.
        request.Topic = "TEST_稽核更新後主題";
        request.Description = "稽核更新後說明";
        Assert.True(await _repository.UpdateAsync(request));

        var updates = (await AuditRowsAsync(pkid)).Where(a => a.ActionType == "Update").ToList();
        var update = Assert.Single(updates);
        Assert.Equal("Topic,Description", update.ActionDesc);   // exactly the two changed columns, in order
        Assert.Equal("system", update.UserName);
    }

    // ---------- Delete → one Delete row, ActionDesc = the removed row's first string column ----------

    [IntegrationFact]
    public async Task DeleteAsync_WritesDeleteAuditRow()
    {
        var pkid = await InsertAsync(NewRequest(new DateOnly(2099, 8, 17), slot: 1));

        Assert.True(await _repository.DeleteAsync(pkid));

        var deletes = (await AuditRowsAsync(pkid)).Where(a => a.ActionType == "Delete").ToList();
        var delete = Assert.Single(deletes);
        Assert.Equal(PromoTopic, delete.ActionDesc);
        Assert.Equal(pkid.ToString(), delete.PrimaryKeyValues);
    }

    // ---------- A failed change leaves NO audit row (same-transaction atomicity) ----------

    [IntegrationFact]
    public async Task FailedInsert_WritesNoAuditRow()
    {
        var date = new DateOnly(2099, 8, 24);
        await InsertAsync(NewRequest(date, slot: 1));   // occupies (date, center, slot 1)

        var before = await TotalAuditRowsAsync();

        // A second insert into the same (ScheduleOn, TrainingCenter, Slot) trips the UNIQUE index; the
        // repository's transaction rolls back, so neither the row nor its audit entry is written.
        await Assert.ThrowsAsync<SqlException>(() => _repository.InsertAsync(NewRequest(date, slot: 1)));

        Assert.Equal(before, await TotalAuditRowsAsync());
    }
}
