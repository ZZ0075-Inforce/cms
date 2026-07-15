using System.Data.Common;
using CMS.API.Data;
using CMS.API.Infrastructure;
using CMS.API.Models;
using Dapper;

namespace CMS.API.Repositories;

public sealed class AppRoleRepository(IDbConnectionFactory connectionFactory) : IAppRoleRepository
{
    // 使用者數 — AppUserRole joins on the string key RoleId, not pkid.
    private const string SelectColumns = """
        SELECT r.pkid AS Pkid, r.RoleId, r.RoleName, r.PermissionLevel, r.Description,
               (SELECT COUNT(*) FROM dbo.AppUserRole ur WHERE ur.RoleId = r.RoleId) AS UserCount
        FROM   dbo.AppRole r
        """;

    public async Task<IReadOnlyList<AppRole>> GetAllAsync(CancellationToken ct = default)
    {
        const string sql = $"{SelectColumns} ORDER BY r.RoleId ASC;";

        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<AppRole>(new CommandDefinition(sql, cancellationToken: ct));
        return rows.AsList();
    }

    public async Task<IReadOnlyList<AppRole>> QueryAsync(AppRoleQuery query, CancellationToken ct = default)
    {
        // One static statement with null-guarded predicates — no SQL string building.
        const string sql = $"""
            {SelectColumns}
            WHERE  (@Keyword IS NULL
                    OR r.RoleId      LIKE @Like ESCAPE '\'
                    OR r.RoleName    LIKE @Like ESCAPE '\'
                    OR r.Description LIKE @Like ESCAPE '\')
              AND  (@PermissionLevel IS NULL OR r.PermissionLevel = @PermissionLevel)
            ORDER  BY r.RoleId ASC;
            """;

        var keyword = string.IsNullOrWhiteSpace(query.Keyword) ? null : query.Keyword.Trim();

        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<AppRole>(new CommandDefinition(
            sql,
            new
            {
                Keyword = keyword,
                Like = keyword is null ? null : SqlLike.ToPattern(keyword),
                query.PermissionLevel
            },
            cancellationToken: ct));
        return rows.AsList();
    }

    public async Task<AppRole?> GetByIdAsync(string roleId, CancellationToken ct = default)
    {
        const string sql = $"""
            {SelectColumns}
            WHERE r.RoleId = @RoleId;

            SELECT ur.UserId
            FROM   dbo.AppUserRole ur
            WHERE  ur.RoleId = @RoleId
            ORDER  BY ur.UserId;
            """;

        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var multi = await conn.QueryMultipleAsync(new CommandDefinition(
            sql, new { RoleId = roleId }, cancellationToken: ct));

        var role = await multi.ReadSingleOrDefaultAsync<AppRole>();
        if (role is null) return null;

        role.UserIds = (await multi.ReadAsync<string>()).AsList();
        return role;
    }

    public async Task<bool> ExistsAsync(string roleId, CancellationToken ct = default)
    {
        const string sql = "SELECT CASE WHEN EXISTS (SELECT 1 FROM dbo.AppRole WHERE RoleId = @RoleId) THEN 1 ELSE 0 END;";

        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        return await conn.ExecuteScalarAsync<bool>(new CommandDefinition(
            sql, new { RoleId = roleId }, cancellationToken: ct));
    }

    public async Task<int> InsertAsync(AppRoleRequest request, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO dbo.AppRole (RoleId, RoleName, PermissionLevel, Description)
            VALUES (@RoleId, @RoleName, @PermissionLevel, @Description);
            SELECT CAST(SCOPE_IDENTITY() AS int);
            """;

        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        var pkid = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            sql,
            new { request.RoleId, request.RoleName, request.PermissionLevel, request.Description },
            tx, cancellationToken: ct));

        await SyncUserRolesAsync(conn, tx, request.RoleId, request.UserIds, ct);

        await tx.CommitAsync(ct);
        return pkid;
    }

    public async Task<bool> UpdateAsync(AppRoleRequest request, CancellationToken ct = default)
    {
        // RoleId is the key and is immutable — never in the SET list. Renaming it would orphan
        // AppUserRole rows (FK_AppUserRole_AppRole has no ON UPDATE CASCADE).
        const string sql = """
            UPDATE dbo.AppRole
            SET    RoleName = @RoleName,
                   PermissionLevel = @PermissionLevel,
                   Description = @Description
            WHERE  RoleId = @RoleId;
            """;

        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        var affected = await conn.ExecuteAsync(new CommandDefinition(
            sql,
            new { request.RoleId, request.RoleName, request.PermissionLevel, request.Description },
            tx, cancellationToken: ct));

        if (affected == 0)
        {
            await tx.RollbackAsync(ct);
            return false;
        }

        await SyncUserRolesAsync(conn, tx, request.RoleId, request.UserIds, ct);

        await tx.CommitAsync(ct);
        return true;
    }

    public async Task<bool> DeleteAsync(string roleId, CancellationToken ct = default)
    {
        // FK_AppUserRole_AppRole has no ON DELETE CASCADE, so junction rows must go first.
        const string deleteUserRoles = "DELETE FROM dbo.AppUserRole WHERE RoleId = @RoleId;";
        const string deleteRole = "DELETE FROM dbo.AppRole WHERE RoleId = @RoleId;";

        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        await conn.ExecuteAsync(new CommandDefinition(
            deleteUserRoles, new { RoleId = roleId }, tx, cancellationToken: ct));

        var affected = await conn.ExecuteAsync(new CommandDefinition(
            deleteRole, new { RoleId = roleId }, tx, cancellationToken: ct));

        if (affected == 0)
        {
            await tx.RollbackAsync(ct);
            return false;
        }

        await tx.CommitAsync(ct);
        return true;
    }

    /// <summary>N-N sync: delete-then-reinsert, inside the caller's transaction.</summary>
    private static async Task SyncUserRolesAsync(
        DbConnection conn, DbTransaction tx, string roleId, List<string>? userIds, CancellationToken ct)
    {
        await conn.ExecuteAsync(new CommandDefinition(
            "DELETE FROM dbo.AppUserRole WHERE RoleId = @RoleId;",
            new { RoleId = roleId }, tx, cancellationToken: ct));

        // Distinct is required: PK_AppUserRole(UserId, RoleId) means a duplicate in the payload
        // would throw 2627 and abort the whole save.
        var ids = (userIds ?? [])
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .Select(u => u.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (ids.Count == 0) return;

        await conn.ExecuteAsync(new CommandDefinition(
            "INSERT INTO dbo.AppUserRole (UserId, RoleId) VALUES (@UserId, @RoleId);",
            ids.Select(u => new { UserId = u, RoleId = roleId }),
            tx, cancellationToken: ct));
    }
}
