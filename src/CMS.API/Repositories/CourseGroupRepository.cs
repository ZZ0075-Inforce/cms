using CMS.API.Data;
using CMS.API.Infrastructure;
using CMS.API.Models;
using Dapper;

namespace CMS.API.Repositories;

public sealed class CourseGroupRepository(IDbConnectionFactory connectionFactory) : ICourseGroupRepository
{
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
        // No transaction: CourseGroup has no junction rows to keep in step, so this is a single
        // statement. SCOPE_IDENTITY() returns numeric(38,0), hence the CAST to the pkid's own type.
        const string sql = """
            INSERT INTO dbo.CourseGroup (Description)
            VALUES (@Description);
            SELECT CAST(SCOPE_IDENTITY() AS smallint);
            """;

        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        return await conn.ExecuteScalarAsync<short>(new CommandDefinition(
            sql, Parameters(request), cancellationToken: ct));
    }

    public async Task<bool> UpdateAsync(CourseGroupRequest request, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE dbo.CourseGroup
            SET    Description = @Description
            WHERE  pkid = @Pkid;
            """;

        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        var affected = await conn.ExecuteAsync(new CommandDefinition(
            sql, Parameters(request, request.Pkid), cancellationToken: ct));
        return affected > 0;
    }

    public async Task<bool> DeleteAsync(short pkid, CancellationToken ct = default)
    {
        // FK_Course_CourseGroup cascades, so any Course rows in the group are deleted along with it.
        // FK_PartnerCourseGroup_CourseGroup does NOT cascade, so a group still referenced by a
        // PartnerCourseGroup row throws 547. That is deliberately left to propagate: the controller
        // turns it into a 409. (The Course cascade cannot be blocked here — the DB owns it.)
        const string sql = "DELETE FROM dbo.CourseGroup WHERE pkid = @Pkid;";

        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        var affected = await conn.ExecuteAsync(new CommandDefinition(
            sql, new { Pkid = pkid }, cancellationToken: ct));
        return affected > 0;
    }

    private static object Parameters(CourseGroupRequest request, short? pkid = null) => new
    {
        Pkid = pkid,
        Description = request.Description.Trim()
    };
}
