using CMS.API.Models;
using CMS.API.Repositories;
using CMS.API.Tests.Infrastructure;
using Dapper;

namespace CMS.API.Tests.Repositories;

/// <summary>
/// Proves the read side of the audit trail against the real database: GetForRecordAsync filters by
/// TableName AND PrimaryKeyValues, and returns the matching rows newest first.
///
/// It seeds its own RowAudit rows under a unique TEST_ TableName (RowAudit has no FK, so a synthetic
/// TableName is free of side effects) and deletes exactly those rows on teardown — the shared
/// DatabaseFixture cleanup only sweeps the six business tables, never RowAudit.
///
/// Run without a DB:  dotnet test --filter "Category!=Integration"
/// </summary>
[Trait("Category", "Integration")]
[Collection(DatabaseCollection.Name)]
public class RowAuditRepositoryIntegrationTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private readonly RowAuditRepository _repository = new(fixture.ConnectionFactory);

    // Two unique synthetic table names so filtering by TableName can be proven, and cleanup is precise.
    private readonly string _table = $"{DatabaseFixture.Prefix}Audit_{Guid.NewGuid():N}";
    private readonly string _otherTable = $"{DatabaseFixture.Prefix}Audit_{Guid.NewGuid():N}";

    private const string RecordPkid = "100";
    private const string OtherRecordPkid = "200";

    private static readonly DateTime T0 = new(2026, 6, 1, 9, 0, 0);
    private static readonly DateTime T1 = new(2026, 6, 4, 14, 30, 0);
    private static readonly DateTime T2 = new(2026, 6, 10, 8, 15, 0);

    public async Task InitializeAsync()
    {
        if (!DatabaseProbe.IsAvailable) return;

        await using var conn = await fixture.OpenAsync();

        // Three rows for the target record, inserted OUT of chronological order so the ORDER BY (not
        // the insert order) is what produces newest-first. Plus two rows that must be filtered OUT:
        // same record pkid under a different table, and a different pkid under the same table.
        await conn.ExecuteAsync("""
            INSERT INTO dbo.RowAudit (TableName, UserName, PrimaryKeyValues, ActionType, ActionDesc, [DateTime])
            VALUES (@Table, 'bob',   @Pkid,      'Insert', 'Original title', @T1),
                   (@Table, 'alice', @Pkid,      'Update', 'Title',          @T2),
                   (@Table, 'carol', @Pkid,      'Update', 'Description',     @T0),
                   (@Table, 'dave',  @OtherPkid, 'Insert', 'Another record',  @T2),
                   (@Other, 'erin',  @Pkid,      'Insert', 'Different table',  @T2);
            """, new
        {
            Table = _table,
            Other = _otherTable,
            Pkid = RecordPkid,
            OtherPkid = OtherRecordPkid,
            T0, T1, T2
        });
    }

    public async Task DisposeAsync()
    {
        if (!DatabaseProbe.IsAvailable) return;

        await using var conn = await fixture.OpenAsync();
        await conn.ExecuteAsync(
            "DELETE FROM dbo.RowAudit WHERE TableName IN @Tables;",
            new { Tables = new[] { _table, _otherTable } });
    }

    [IntegrationFact]
    public async Task GetForRecordAsync_ReturnsMatchingRows_NewestFirst()
    {
        var rows = await _repository.GetForRecordAsync(_table, RecordPkid);

        // Only the three rows for (this table, this pkid) — the other-table and other-pkid rows excluded.
        Assert.Equal(3, rows.Count);
        Assert.Equal(new[] { T2, T1, T0 }, rows.Select(r => r.DateTime));      // strictly newest first
        Assert.Equal("Update", rows[0].ActionType);
        Assert.Equal("alice", rows[0].UserName);
        Assert.Equal("Title", rows[0].ActionDesc);
    }

    [IntegrationFact]
    public async Task GetForRecordAsync_FiltersByTableName()
    {
        var rows = await _repository.GetForRecordAsync(_otherTable, RecordPkid);

        var only = Assert.Single(rows);
        Assert.Equal("erin", only.UserName);          // never leaks the same-pkid row from _table
    }

    [IntegrationFact]
    public async Task GetForRecordAsync_ReturnsEmpty_WhenRecordHasNoHistory()
    {
        var rows = await _repository.GetForRecordAsync(_table, "does-not-exist");

        Assert.Empty(rows);
    }
}
