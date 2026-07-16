using System.Data.Common;
using CMS.API.Data;
using CMS.API.Infrastructure;
using CMS.API.Models;
using Dapper;

namespace CMS.API.Repositories;

public sealed class PartnerRepository(IDbConnectionFactory connectionFactory, IRowAuditWriter auditWriter)
    : IPartnerRepository
{
    private const string AuditTable = "Partner";
    // No JOIN: Partner has no foreign keys. CourseCount is 對應課程數, for display in the list.
    private const string SelectColumns = """
        SELECT p.pkid AS Pkid, p.Name, p.AppKey, p.NameOnPartnerMenu,
               p.NameOnCourseDetailPage, p.DisplayOrder, p.ImageFilename,
               (SELECT COUNT(*) FROM dbo.Course c WHERE c.Partner_pkid = p.pkid) AS CourseCount
        FROM   dbo.Partner p
        """;

    private const string OrderBy = "ORDER BY p.DisplayOrder ASC, p.pkid ASC";

    public async Task<IReadOnlyList<Partner>> GetAllAsync(CancellationToken ct = default)
    {
        const string sql = $"{SelectColumns} {OrderBy};";

        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<Partner>(new CommandDefinition(sql, cancellationToken: ct));
        return rows.AsList();
    }

    public async Task<IReadOnlyList<Partner>> QueryAsync(PartnerQuery query, CancellationToken ct = default)
    {
        // One static statement with a null-guarded predicate — no SQL string building.
        const string sql = $"""
            {SelectColumns}
            WHERE (@Keyword IS NULL
                   OR p.Name                   LIKE @Like ESCAPE '\'
                   OR p.AppKey                 LIKE @Like ESCAPE '\'
                   OR p.NameOnPartnerMenu      LIKE @Like ESCAPE '\'
                   OR p.NameOnCourseDetailPage LIKE @Like ESCAPE '\')
            {OrderBy};
            """;

        var keyword = string.IsNullOrWhiteSpace(query.Keyword) ? null : query.Keyword.Trim();

        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<Partner>(new CommandDefinition(
            sql,
            new
            {
                Keyword = keyword,
                Like = keyword is null ? null : SqlLike.ToPattern(keyword)
            },
            cancellationToken: ct));
        return rows.AsList();
    }

    public async Task<Partner?> GetByIdAsync(short pkid, CancellationToken ct = default)
    {
        const string sql = $"{SelectColumns} WHERE p.pkid = @Pkid;";

        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<Partner>(new CommandDefinition(
            sql, new { Pkid = pkid }, cancellationToken: ct));
    }

    public async Task<short> InsertAsync(PartnerRequest request, CancellationToken ct = default)
    {
        // Partner has no junction rows, but the insert is wrapped in a transaction so the RowAudit row
        // commits or rolls back atomically with it. SCOPE_IDENTITY() returns numeric(38,0), hence the
        // CAST to the pkid's own type.
        const string sql = """
            INSERT INTO dbo.Partner (Name, AppKey, NameOnPartnerMenu, NameOnCourseDetailPage,
                                     DisplayOrder, ImageFilename)
            VALUES (@Name, @AppKey, @NameOnPartnerMenu, @NameOnCourseDetailPage,
                    @DisplayOrder, @ImageFilename);
            SELECT CAST(SCOPE_IDENTITY() AS smallint);
            """;

        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        var pkid = await conn.ExecuteScalarAsync<short>(new CommandDefinition(
            sql, Parameters(request), tx, cancellationToken: ct));

        var inserted = await LoadForAuditAsync(conn, tx, pkid, ct);
        await auditWriter.LogInsertAsync(conn, tx, AuditTable, inserted!, ct);

        await tx.CommitAsync(ct);
        return pkid;
    }

    public async Task<bool> UpdateAsync(PartnerRequest request, CancellationToken ct = default)
    {
        // Every non-IDENTITY column is writable, AppKey included — it is not a key and nothing
        // references it, so renaming it orphans nothing.
        const string sql = """
            UPDATE dbo.Partner
            SET    Name = @Name,
                   AppKey = @AppKey,
                   NameOnPartnerMenu = @NameOnPartnerMenu,
                   NameOnCourseDetailPage = @NameOnCourseDetailPage,
                   DisplayOrder = @DisplayOrder,
                   ImageFilename = @ImageFilename
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

        await conn.ExecuteAsync(new CommandDefinition(
            sql, Parameters(request, request.Pkid), tx, cancellationToken: ct));

        var after = await LoadForAuditAsync(conn, tx, request.Pkid, ct);
        await auditWriter.LogUpdateAsync(conn, tx, AuditTable, before, after!, ct);

        await tx.CommitAsync(ct);
        return true;
    }

    public async Task<bool> DeleteAsync(short pkid, CancellationToken ct = default)
    {
        // Course / Certification / PartnerCourseGroup / Promotion2 / Seminar all FK to Partner and
        // none of them cascade, so a partner still in use throws 547. That is deliberately left to
        // propagate: the controller turns it into a 409, and the transaction rolls back — so a blocked
        // delete leaves no audit row.
        const string sql = "DELETE FROM dbo.Partner WHERE pkid = @Pkid;";

        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        // Load before deleting so the audit can record the row's first string column (Name).
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
    private static async Task<Partner?> LoadForAuditAsync(
        DbConnection conn, DbTransaction tx, short pkid, CancellationToken ct)
        => await conn.QuerySingleOrDefaultAsync<Partner>(new CommandDefinition("""
            SELECT pkid AS Pkid, Name, AppKey, NameOnPartnerMenu, NameOnCourseDetailPage,
                   DisplayOrder, ImageFilename
            FROM   dbo.Partner
            WHERE  pkid = @Pkid;
            """, new { Pkid = pkid }, tx, cancellationToken: ct));

    private static object Parameters(PartnerRequest request, short? pkid = null) => new
    {
        Pkid = pkid,
        request.Name,
        request.AppKey,
        request.NameOnPartnerMenu,
        request.NameOnCourseDetailPage,
        request.DisplayOrder,
        // A blank filename would otherwise land in the DB as '' rather than NULL.
        ImageFilename = string.IsNullOrWhiteSpace(request.ImageFilename)
            ? null
            : request.ImageFilename.Trim()
    };
}
