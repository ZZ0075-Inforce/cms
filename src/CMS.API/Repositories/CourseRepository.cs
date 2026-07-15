using System.Data.Common;
using CMS.API.Data;
using CMS.API.Infrastructure;
using CMS.API.Models;
using Dapper;

namespace CMS.API.Repositories;

public sealed class CourseRepository(IDbConnectionFactory connectionFactory) : ICourseRepository
{
    // Three FKs resolved to flat display labels: INNER JOIN Partner/PublishStatus (both NOT NULL),
    // LEFT JOIN CourseGroup (nullable → CourseGroupName comes back null when no group).
    private const string SelectColumns = """
        SELECT c.pkid AS Pkid, c.Title, c.OfficialTitle, c.CourseId, c.ProdCourseId, c.FriendlyUrl,
               c.DisplayOrder,
               c.Partner_pkid       AS PartnerPkid,
               c.CourseGroup_pkid   AS CourseGroupPkid,
               c.PublishStatus_pkid AS PublishStatusPkid,
               c.ScheduleOn, c.ScheduleOff, c.Hour, c.ListPrice, c.LearningCredit,
               c.Material, c.Objective, c.Target, c.Prerequisites, c.Outline,
               c.TowardCertOrExam, c.Note, c.OtherInfo, c.CanRepeat,
               p.Name        AS PartnerName,
               g.Description AS CourseGroupName,
               s.Description AS PublishStatusName
        FROM        dbo.Course c
        INNER JOIN  dbo.Partner       p ON p.pkid = c.Partner_pkid
        LEFT  JOIN  dbo.CourseGroup   g ON g.pkid = c.CourseGroup_pkid
        INNER JOIN  dbo.PublishStatus s ON s.pkid = c.PublishStatus_pkid
        """;

    private const string OrderBy = "ORDER BY c.DisplayOrder ASC, c.pkid ASC";

    public async Task<IReadOnlyList<Course>> GetAllAsync(CancellationToken ct = default)
    {
        const string sql = $"{SelectColumns} {OrderBy};";

        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<Course>(new CommandDefinition(sql, cancellationToken: ct));
        return rows.AsList();
    }

    public async Task<IReadOnlyList<Course>> QueryAsync(CourseQuery query, CancellationToken ct = default)
    {
        // One static statement; every predicate is null-guarded so an omitted filter drops out.
        const string sql = $"""
            {SelectColumns}
            WHERE (@Keyword IS NULL
                   OR c.Title         LIKE @Like ESCAPE '\'
                   OR c.OfficialTitle LIKE @Like ESCAPE '\'
                   OR c.CourseId      LIKE @Like ESCAPE '\'
                   OR c.ProdCourseId  LIKE @Like ESCAPE '\'
                   OR c.FriendlyUrl   LIKE @Like ESCAPE '\')
              AND (@PartnerPkid       IS NULL OR c.Partner_pkid       = @PartnerPkid)
              AND (@CourseGroupPkid   IS NULL OR c.CourseGroup_pkid   = @CourseGroupPkid)
              AND (@PublishStatusPkid IS NULL OR c.PublishStatus_pkid = @PublishStatusPkid)
              AND (@CanRepeat         IS NULL OR c.CanRepeat          = @CanRepeat)
              AND (@ScheduleOnFrom    IS NULL OR c.ScheduleOn  >= @ScheduleOnFrom)
              AND (@ScheduleOnTo      IS NULL OR c.ScheduleOn  <= @ScheduleOnTo)
              AND (@ScheduleOffFrom   IS NULL OR c.ScheduleOff >= @ScheduleOffFrom)
              AND (@ScheduleOffTo     IS NULL OR c.ScheduleOff <= @ScheduleOffTo)
            {OrderBy};
            """;

        var keyword = string.IsNullOrWhiteSpace(query.Keyword) ? null : query.Keyword.Trim();

        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<Course>(new CommandDefinition(
            sql,
            new
            {
                Keyword = keyword,
                Like = keyword is null ? null : SqlLike.ToPattern(keyword),
                query.PartnerPkid,
                query.CourseGroupPkid,
                query.PublishStatusPkid,
                query.CanRepeat,
                query.ScheduleOnFrom,
                query.ScheduleOnTo,
                query.ScheduleOffFrom,
                query.ScheduleOffTo
            },
            cancellationToken: ct));
        return rows.AsList();
    }

    public async Task<Course?> GetByIdAsync(int pkid, CancellationToken ct = default)
    {
        const string sql = $"""
            {SelectColumns}
            WHERE c.pkid = @Pkid;

            SELECT Certification_pkid FROM dbo.CourseInCertification WHERE Course_pkid = @Pkid ORDER BY Certification_pkid;
            SELECT JobCategory_pkid   FROM dbo.CourseJobCategories   WHERE Course_pkid = @Pkid ORDER BY JobCategory_pkid;
            """;

        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var multi = await conn.QueryMultipleAsync(new CommandDefinition(
            sql, new { Pkid = pkid }, cancellationToken: ct));

        var course = await multi.ReadSingleOrDefaultAsync<Course>();
        if (course is null) return null;

        course.CertificationPkids = (await multi.ReadAsync<int>()).AsList();
        course.JobCategoryPkids = (await multi.ReadAsync<short>()).AsList();
        return course;
    }

    public async Task<int> InsertAsync(CourseRequest request, CancellationToken ct = default)
    {
        // Transaction spans the scalar insert and both N-N syncs, exactly like AppRoleRepository:
        // a junction failure must roll back the course row too. SCOPE_IDENTITY() is numeric(38,0).
        const string sql = """
            INSERT INTO dbo.Course
                (Title, OfficialTitle, CourseId, ProdCourseId, FriendlyUrl, DisplayOrder,
                 Partner_pkid, CourseGroup_pkid, PublishStatus_pkid, ScheduleOn, ScheduleOff,
                 Hour, ListPrice, LearningCredit, Material, Objective, Target, Prerequisites,
                 Outline, TowardCertOrExam, Note, OtherInfo, CanRepeat)
            VALUES
                (@Title, @OfficialTitle, @CourseId, @ProdCourseId, @FriendlyUrl, @DisplayOrder,
                 @PartnerPkid, @CourseGroupPkid, @PublishStatusPkid, @ScheduleOn, @ScheduleOff,
                 @Hour, @ListPrice, @LearningCredit, @Material, @Objective, @Target, @Prerequisites,
                 @Outline, @TowardCertOrExam, @Note, @OtherInfo, @CanRepeat);
            SELECT CAST(SCOPE_IDENTITY() AS int);
            """;

        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        var pkid = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            sql, Parameters(request), tx, cancellationToken: ct));

        await SyncJunctionAsync(conn, tx, "CourseInCertification", "Certification_pkid",
            pkid, request.CertificationPkids, ct);
        await SyncJunctionAsync(conn, tx, "CourseJobCategories", "JobCategory_pkid",
            pkid, request.JobCategoryPkids, ct);

        await tx.CommitAsync(ct);
        return pkid;
    }

    public async Task<bool> UpdateAsync(CourseRequest request, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE dbo.Course
            SET    Title = @Title, OfficialTitle = @OfficialTitle, CourseId = @CourseId,
                   ProdCourseId = @ProdCourseId, FriendlyUrl = @FriendlyUrl, DisplayOrder = @DisplayOrder,
                   Partner_pkid = @PartnerPkid, CourseGroup_pkid = @CourseGroupPkid,
                   PublishStatus_pkid = @PublishStatusPkid, ScheduleOn = @ScheduleOn, ScheduleOff = @ScheduleOff,
                   Hour = @Hour, ListPrice = @ListPrice, LearningCredit = @LearningCredit,
                   Material = @Material, Objective = @Objective, Target = @Target,
                   Prerequisites = @Prerequisites, Outline = @Outline, TowardCertOrExam = @TowardCertOrExam,
                   Note = @Note, OtherInfo = @OtherInfo, CanRepeat = @CanRepeat
            WHERE  pkid = @Pkid;
            """;

        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        var affected = await conn.ExecuteAsync(new CommandDefinition(
            sql, Parameters(request, request.Pkid), tx, cancellationToken: ct));

        if (affected == 0)
        {
            await tx.RollbackAsync(ct);
            return false;
        }

        await SyncJunctionAsync(conn, tx, "CourseInCertification", "Certification_pkid",
            request.Pkid, request.CertificationPkids, ct);
        await SyncJunctionAsync(conn, tx, "CourseJobCategories", "JobCategory_pkid",
            request.Pkid, request.JobCategoryPkids, ct);

        await tx.CommitAsync(ct);
        return true;
    }

    public async Task<bool> DeleteAsync(int pkid, CancellationToken ct = default)
    {
        // CourseInCertification / CourseJobCategories cascade, so no need to pre-clean them. But
        // CourseFAQ / CourseRelatedLink / HotCourse do NOT cascade — a course still referenced there
        // throws 547, left to propagate for the controller to turn into a 409.
        const string sql = "DELETE FROM dbo.Course WHERE pkid = @Pkid;";

        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        var affected = await conn.ExecuteAsync(new CommandDefinition(
            sql, new { Pkid = pkid }, cancellationToken: ct));
        return affected > 0;
    }

    /// <summary>
    /// N-N sync: delete-then-reinsert inside the caller's transaction. <paramref name="table"/> and
    /// <paramref name="column"/> are compile-time constants, never user input, so interpolating them
    /// is safe; the pkid values are always parameterised.
    /// </summary>
    private static async Task SyncJunctionAsync<T>(
        DbConnection conn, DbTransaction tx, string table, string column,
        int coursePkid, IEnumerable<T>? pkids, CancellationToken ct)
    {
        await conn.ExecuteAsync(new CommandDefinition(
            $"DELETE FROM dbo.{table} WHERE Course_pkid = @CoursePkid;",
            new { CoursePkid = coursePkid }, tx, cancellationToken: ct));

        // Distinct is required: the composite PK (Course_pkid, {column}) throws 2627 on a duplicate
        // in the payload, which would abort the whole save.
        var ids = (pkids ?? []).Distinct().ToList();
        if (ids.Count == 0) return;

        await conn.ExecuteAsync(new CommandDefinition(
            $"INSERT INTO dbo.{table} (Course_pkid, {column}) VALUES (@CoursePkid, @Pkid);",
            ids.Select(id => new { CoursePkid = coursePkid, Pkid = id }),
            tx, cancellationToken: ct));
    }

    private static object Parameters(CourseRequest r, int? pkid = null) => new
    {
        Pkid = pkid,
        Title = r.Title.Trim(),
        OfficialTitle = Blank(r.OfficialTitle),
        CourseId = r.CourseId.Trim(),
        ProdCourseId = r.ProdCourseId.Trim(),
        FriendlyUrl = r.FriendlyUrl.Trim(),
        r.DisplayOrder,
        r.PartnerPkid,
        r.CourseGroupPkid,
        r.PublishStatusPkid,
        r.ScheduleOn,
        r.ScheduleOff,
        r.Hour,
        r.ListPrice,
        r.LearningCredit,
        Material = Blank(r.Material),
        Objective = Blank(r.Objective),
        Target = Blank(r.Target),
        Prerequisites = Blank(r.Prerequisites),
        Outline = Blank(r.Outline),
        TowardCertOrExam = Blank(r.TowardCertOrExam),
        Note = Blank(r.Note),
        OtherInfo = Blank(r.OtherInfo),
        r.CanRepeat
    };

    /// <summary>Blank optional text lands as NULL, not an empty string.</summary>
    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
