using CMS.API.Data;
using CMS.API.Models.Lookups;
using Dapper;

namespace CMS.API.Repositories;

public sealed class LookupRepository(IDbConnectionFactory connectionFactory) : ILookupRepository
{
    public async Task<IReadOnlyList<AppUserLookup>> GetAppUsersAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT u.UserId, u.UserName, u.IsActive
            FROM   dbo.AppUser u
            ORDER  BY u.UserName;
            """;

        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<AppUserLookup>(new CommandDefinition(sql, cancellationToken: ct));
        return rows.AsList();
    }

    public async Task<IReadOnlyList<AppRoleLookup>> GetAppRolesAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT r.RoleId, r.RoleName
            FROM   dbo.AppRole r
            ORDER  BY r.RoleId;
            """;

        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<AppRoleLookup>(new CommandDefinition(sql, cancellationToken: ct));
        return rows.AsList();
    }

    public async Task<IReadOnlyList<PartnerLookup>> GetPartnersAsync(CancellationToken ct = default)
    {
        // Same order as the Partner list itself, so a dropdown and the grid agree.
        const string sql = """
            SELECT p.pkid AS Pkid, p.Name
            FROM   dbo.Partner p
            ORDER  BY p.DisplayOrder ASC, p.pkid ASC;
            """;

        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<PartnerLookup>(new CommandDefinition(sql, cancellationToken: ct));
        return rows.AsList();
    }

    public async Task<IReadOnlyList<CourseGroupLookup>> GetCourseGroupsAsync(CancellationToken ct = default)
    {
        // Alphabetical by name so the dropdown is easy to scan — deliberately unlike the
        // CourseGroup list itself, which defaults to pkid DESC.
        const string sql = """
            SELECT g.pkid AS Pkid, g.Description
            FROM   dbo.CourseGroup g
            ORDER  BY g.Description ASC, g.pkid ASC;
            """;

        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<CourseGroupLookup>(new CommandDefinition(sql, cancellationToken: ct));
        return rows.AsList();
    }

    public async Task<IReadOnlyList<PublishStatusLookup>> GetPublishStatusesAsync(CancellationToken ct = default)
    {
        // Fixed enum-like table; pkid order is the natural draft→published→discontinued progression.
        const string sql = """
            SELECT s.pkid AS Pkid, s.Description
            FROM   dbo.PublishStatus s
            ORDER  BY s.pkid ASC;
            """;

        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<PublishStatusLookup>(new CommandDefinition(sql, cancellationToken: ct));
        return rows.AsList();
    }

    public async Task<IReadOnlyList<CertificationLookup>> GetCertificationsAsync(CancellationToken ct = default)
    {
        // Certification.Title is nchar(100): RTRIM or every label carries trailing padding spaces.
        const string sql = """
            SELECT c.pkid AS Pkid, RTRIM(c.Title) AS Title
            FROM   dbo.Certification c
            ORDER  BY c.Title ASC;
            """;

        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<CertificationLookup>(new CommandDefinition(sql, cancellationToken: ct));
        return rows.AsList();
    }

    public async Task<IReadOnlyList<JobCategoryLookup>> GetJobCategoriesAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT j.pkid AS Pkid, j.Description
            FROM   dbo.JobCategory j
            ORDER  BY j.Description ASC;
            """;

        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<JobCategoryLookup>(new CommandDefinition(sql, cancellationToken: ct));
        return rows.AsList();
    }

    public async Task<IReadOnlyList<TrainingCenterLookup>> GetTrainingCentersAsync(CancellationToken ct = default)
    {
        // DisplayOrder is the intended tab order on the board (台北 / 新竹 / 台中 / 高雄 / 線上研討會).
        const string sql = """
            SELECT t.pkid AS Pkid, t.Name
            FROM   dbo.TrainingCenter t
            ORDER  BY t.DisplayOrder ASC, t.pkid ASC;
            """;

        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<TrainingCenterLookup>(new CommandDefinition(sql, cancellationToken: ct));
        return rows.AsList();
    }

    public async Task<PromotionLookup?> GetPromotionByCodeAsync(string promoCode, CancellationToken ct = default)
    {
        // PromoCode is unique (IX_Promotion2_UniquePromoCode), so an exact match is at most one row.
        const string sql = """
            SELECT p.pkid AS Pkid, p.PromoCode, p.Topic, p.Description
            FROM   dbo.Promotion2 p
            WHERE  p.PromoCode = @PromoCode;
            """;

        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<PromotionLookup>(
            new CommandDefinition(sql, new { PromoCode = promoCode.Trim() }, cancellationToken: ct));
    }
}
