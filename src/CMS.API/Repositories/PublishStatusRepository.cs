using System.Data.Common;
using CMS.API.Data;
using CMS.API.Infrastructure;
using CMS.API.Models;
using Dapper;

namespace CMS.API.Repositories;

public sealed class PublishStatusRepository(IDbConnectionFactory connectionFactory, IRowAuditWriter auditWriter)
    : IPublishStatusRepository
{
    private const string AuditTable = "PublishStatus";

    // No JOIN: PublishStatus has no foreign keys. CourseCount is 對應課程數, for display in the list.
    private const string SelectColumns = """
        SELECT s.pkid AS Pkid, s.Description, s.IsDraft, s.IsPublished, s.IsDiscontinued,
               (SELECT COUNT(*) FROM dbo.Course c WHERE c.PublishStatus_pkid = s.pkid) AS CourseCount
        FROM   dbo.PublishStatus s
        """;

    private const string OrderBy = "ORDER BY s.pkid ASC";

    public async Task<IReadOnlyList<PublishStatus>> GetAllAsync(CancellationToken ct = default)
    {
        const string sql = $"{SelectColumns} {OrderBy};";

        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<PublishStatus>(new CommandDefinition(sql, cancellationToken: ct));
        return rows.AsList();
    }

    public async Task<IReadOnlyList<PublishStatus>> QueryAsync(PublishStatusQuery query, CancellationToken ct = default)
    {
        // One static statement with null-guarded predicates — no SQL string building. Each tri-state
        // bit filter is skipped when its parameter is null.
        const string sql = $"""
            {SelectColumns}
            WHERE (@Keyword IS NULL OR s.Description LIKE @Like ESCAPE '\')
              AND (@IsDraft        IS NULL OR s.IsDraft        = @IsDraft)
              AND (@IsPublished    IS NULL OR s.IsPublished    = @IsPublished)
              AND (@IsDiscontinued IS NULL OR s.IsDiscontinued = @IsDiscontinued)
            {OrderBy};
            """;

        var keyword = string.IsNullOrWhiteSpace(query.Keyword) ? null : query.Keyword.Trim();

        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<PublishStatus>(new CommandDefinition(
            sql,
            new
            {
                Keyword = keyword,
                Like = keyword is null ? null : SqlLike.ToPattern(keyword),
                query.IsDraft,
                query.IsPublished,
                query.IsDiscontinued
            },
            cancellationToken: ct));
        return rows.AsList();
    }

    public async Task<PublishStatus?> GetByIdAsync(byte pkid, CancellationToken ct = default)
    {
        const string sql = $"{SelectColumns} WHERE s.pkid = @Pkid;";

        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<PublishStatus>(new CommandDefinition(
            sql, new { Pkid = pkid }, cancellationToken: ct));
    }

    public async Task<bool> ExistsAsync(byte pkid, CancellationToken ct = default)
    {
        const string sql =
            "SELECT CASE WHEN EXISTS (SELECT 1 FROM dbo.PublishStatus WHERE pkid = @Pkid) THEN 1 ELSE 0 END;";

        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        return await conn.ExecuteScalarAsync<bool>(new CommandDefinition(
            sql, new { Pkid = pkid }, cancellationToken: ct));
    }

    public async Task<byte> InsertAsync(PublishStatusRequest request, CancellationToken ct = default)
    {
        // pkid is client-supplied (tinyint, NOT IDENTITY), so it is written explicitly and there is no
        // SCOPE_IDENTITY() to read back. The insert is wrapped in a transaction so the RowAudit row
        // commits or rolls back atomically with it — including the 2627 that a duplicate pkid throws.
        const string sql = """
            INSERT INTO dbo.PublishStatus (pkid, Description, IsDraft, IsPublished, IsDiscontinued)
            VALUES (@Pkid, @Description, @IsDraft, @IsPublished, @IsDiscontinued);
            """;

        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        await conn.ExecuteAsync(new CommandDefinition(sql, Parameters(request), tx, cancellationToken: ct));

        var inserted = await LoadForAuditAsync(conn, tx, request.Pkid, ct);
        await auditWriter.LogInsertAsync(conn, tx, AuditTable, inserted!, ct);

        await tx.CommitAsync(ct);
        return request.Pkid;
    }

    public async Task<bool> UpdateAsync(PublishStatusRequest request, CancellationToken ct = default)
    {
        // pkid is the key and immutable — never in the SET list. Course FKs to it and the FK has no
        // ON UPDATE CASCADE, so renaming the key would orphan Course rows.
        const string sql = """
            UPDATE dbo.PublishStatus
            SET    Description = @Description,
                   IsDraft = @IsDraft,
                   IsPublished = @IsPublished,
                   IsDiscontinued = @IsDiscontinued
            WHERE  pkid = @Pkid;
            """;

        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        // Load the "before" first so the audit's changed-column list is accurate; a missing row is 404.
        var before = await LoadForAuditAsync(conn, tx, request.Pkid, ct);
        if (before is null)
        {
            await tx.RollbackAsync(ct);
            return false;
        }

        await conn.ExecuteAsync(new CommandDefinition(sql, Parameters(request), tx, cancellationToken: ct));

        var after = await LoadForAuditAsync(conn, tx, request.Pkid, ct);
        await auditWriter.LogUpdateAsync(conn, tx, AuditTable, before, after!, ct);

        await tx.CommitAsync(ct);
        return true;
    }

    public async Task<bool> DeleteAsync(byte pkid, CancellationToken ct = default)
    {
        // FK_Course_PublishStatus does not cascade, so a status still referenced by a course throws 547.
        // That is left to propagate: the controller turns it into a 409, and the transaction rolls back
        // — so a blocked delete leaves no audit row.
        const string sql = "DELETE FROM dbo.PublishStatus WHERE pkid = @Pkid;";

        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        // Load before deleting so the audit can record the row's first string column (Description).
        var row = await LoadForAuditAsync(conn, tx, pkid, ct);
        if (row is null)
        {
            await tx.RollbackAsync(ct);
            return false;
        }

        await conn.ExecuteAsync(new CommandDefinition(sql, new { Pkid = pkid }, tx, cancellationToken: ct));
        await auditWriter.LogDeleteAsync(conn, tx, AuditTable, row, ct);

        await tx.CommitAsync(ct);
        return true;
    }

    /// <summary>
    /// Loads the row's own columns (no derived CourseCount) as the audit before/after snapshot, on the
    /// caller's transaction so it sees the in-flight change.
    /// </summary>
    private static async Task<PublishStatus?> LoadForAuditAsync(
        DbConnection conn, DbTransaction tx, byte pkid, CancellationToken ct)
        => await conn.QuerySingleOrDefaultAsync<PublishStatus>(new CommandDefinition("""
            SELECT pkid AS Pkid, Description, IsDraft, IsPublished, IsDiscontinued
            FROM   dbo.PublishStatus
            WHERE  pkid = @Pkid;
            """, new { Pkid = pkid }, tx, cancellationToken: ct));

    private static object Parameters(PublishStatusRequest request) => new
    {
        request.Pkid,
        Description = request.Description.Trim(),
        request.IsDraft,
        request.IsPublished,
        request.IsDiscontinued
    };
}
