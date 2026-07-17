using System.Data.Common;
using CMS.API.Data;
using CMS.API.Infrastructure;
using CMS.API.Models;
using Dapper;

namespace CMS.API.Repositories;

public sealed class CourseGroupRepository(IDbConnectionFactory connectionFactory, IRowAuditWriter auditWriter)
    : ICourseGroupRepository
{
    private const string AuditTable = "CourseGroup";
    /// <summary>Deleting a group cascades to its Courses, which must be audited under their own table name.</summary>
    private const string CourseAuditTable = "Course";
    // No JOIN: CourseGroup has no foreign keys. CourseCount is 對應課程數, for display in the list
    // and to size the delete-confirm warning (FK_Course_CourseGroup cascades).
    private const string SelectColumns = """
        SELECT g.pkid AS Pkid, g.Description,
               (SELECT COUNT(*) FROM dbo.Course c WHERE c.CourseGroup_pkid = g.pkid) AS CourseCount
        FROM   dbo.CourseGroup g
        """;

    // No DisplayOrder or date column on this table; pkid DESC is the convention's fallback.
    private const string OrderBy = "ORDER BY g.pkid DESC";

    public async Task<IReadOnlyList<CourseGroup>> GetAllAsync(CancellationToken ct = default)
    {
        const string sql = $"{SelectColumns} {OrderBy};";

        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<CourseGroup>(new CommandDefinition(sql, cancellationToken: ct));
        return rows.AsList();
    }

    public async Task<IReadOnlyList<CourseGroup>> QueryAsync(CourseGroupQuery query, CancellationToken ct = default)
    {
        // One static statement with a null-guarded predicate — no SQL string building.
        const string sql = $"""
            {SelectColumns}
            WHERE (@Keyword IS NULL OR g.Description LIKE @Like ESCAPE '\')
            {OrderBy};
            """;

        var keyword = string.IsNullOrWhiteSpace(query.Keyword) ? null : query.Keyword.Trim();

        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<CourseGroup>(new CommandDefinition(
            sql,
            new
            {
                Keyword = keyword,
                Like = keyword is null ? null : SqlLike.ToPattern(keyword)
            },
            cancellationToken: ct));
        return rows.AsList();
    }

    public async Task<CourseGroup?> GetByIdAsync(short pkid, CancellationToken ct = default)
    {
        const string sql = $"{SelectColumns} WHERE g.pkid = @Pkid;";

        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<CourseGroup>(new CommandDefinition(
            sql, new { Pkid = pkid }, cancellationToken: ct));
    }

    public async Task<short> InsertAsync(CourseGroupRequest request, CancellationToken ct = default)
    {
        // A transaction (which the plain CRUD path would not otherwise need) makes the RowAudit row
        // commit or roll back atomically with the insert. SCOPE_IDENTITY() returns numeric(38,0),
        // hence the CAST to the pkid's own type.
        const string sql = """
            INSERT INTO dbo.CourseGroup (Description)
            VALUES (@Description);
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

    public async Task<bool> UpdateAsync(CourseGroupRequest request, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE dbo.CourseGroup
            SET    Description = @Description
            WHERE  pkid = @Pkid;
            """;

        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        // Load the "before" first so the audit's changed-column list is accurate; a missing row is the
        // 404 case (equivalent to the old affected == 0).
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
        // FK_Course_CourseGroup cascades, so any Course rows in the group are deleted along with it.
        // FK_PartnerCourseGroup_CourseGroup does NOT cascade, so a group still referenced by a
        // PartnerCourseGroup row throws 547. That is deliberately left to propagate: the controller
        // turns it into a 409, and the transaction rolls back — so no audit row for a blocked delete.
        // (The Course cascade cannot be blocked here — the DB owns it.)
        const string sql = "DELETE FROM dbo.CourseGroup WHERE pkid = @Pkid;";

        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        // Load before deleting so the audit can record the row's first string column (Description).
        var row = await LoadForAuditAsync(conn, tx, pkid, ct);
        if (row is null)
        {
            await tx.RollbackAsync(ct);
            return false;
        }

        // The cascade deletes Course rows that this repository never names, and SQL Server fires no
        // audit of its own — so without loading them FIRST, N courses vanish leaving a trail that
        // mentions only the group. The audit is the compliance record; "the DB did it" is not an
        // answer to "who deleted this course". Read them while they still exist.
        var cascadedCourses = await LoadCascadedCoursesAsync(conn, tx, pkid, ct);

        await conn.ExecuteAsync(new CommandDefinition(sql, new { Pkid = pkid }, tx, cancellationToken: ct));

        await auditWriter.LogDeleteAsync(conn, tx, AuditTable, row, ct);
        // Same conn/tx as the delete, so the trail commits or rolls back with it — a blocked delete
        // (547 from PartnerCourseGroup) must not leave behind audit rows for courses that still exist.
        foreach (var course in cascadedCourses)
            await auditWriter.LogDeleteAsync(conn, tx, CourseAuditTable, course, ct);

        await tx.CommitAsync(ct);
        return true;
    }

    /// <summary>
    /// The Course rows FK_Course_CourseGroup is about to cascade-delete. The projection mirrors
    /// CourseRepository.LoadForAuditAsync column for column, so a course deleted via its group audits
    /// identically to one deleted directly — the trail must not record less just because of the route.
    /// </summary>
    private static async Task<IReadOnlyList<Course>> LoadCascadedCoursesAsync(
        DbConnection conn, DbTransaction tx, short courseGroupPkid, CancellationToken ct)
        => (await conn.QueryAsync<Course>(new CommandDefinition("""
            SELECT c.pkid AS Pkid, c.Title, c.OfficialTitle, c.CourseId, c.ProdCourseId, c.FriendlyUrl,
                   c.DisplayOrder,
                   c.Partner_pkid       AS PartnerPkid,
                   c.CourseGroup_pkid   AS CourseGroupPkid,
                   c.PublishStatus_pkid AS PublishStatusPkid,
                   c.ScheduleOn, c.ScheduleOff, c.Hour, c.ListPrice, c.LearningCredit,
                   c.Material, c.Objective, c.Target, c.Prerequisites, c.Outline,
                   c.TowardCertOrExam, c.Note, c.OtherInfo, c.CanRepeat
            FROM   dbo.Course c
            WHERE  c.CourseGroup_pkid = @CourseGroupPkid;
            """, new { CourseGroupPkid = courseGroupPkid }, tx, cancellationToken: ct))).AsList();

    /// <summary>
    /// Loads the row's own columns (no derived CourseCount) as the audit before/after snapshot, on the
    /// caller's transaction so it sees the in-flight change.
    /// </summary>
    private static async Task<CourseGroup?> LoadForAuditAsync(
        DbConnection conn, DbTransaction tx, short pkid, CancellationToken ct)
        => await conn.QuerySingleOrDefaultAsync<CourseGroup>(new CommandDefinition(
            "SELECT pkid AS Pkid, Description FROM dbo.CourseGroup WHERE pkid = @Pkid;",
            new { Pkid = pkid }, tx, cancellationToken: ct));

    private static object Parameters(CourseGroupRequest request, short? pkid = null) => new
    {
        Pkid = pkid,
        Description = request.Description.Trim()
    };
}
