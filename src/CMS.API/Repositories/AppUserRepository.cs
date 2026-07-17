using System.Data.Common;
using System.Text.Json;
using CMS.API.Data;
using CMS.API.Infrastructure;
using CMS.API.Models;
using Dapper;

namespace CMS.API.Repositories;

public sealed class AppUserRepository(IDbConnectionFactory connectionFactory, IRowAuditWriter auditWriter)
    : IAppUserRepository
{
    private const string AuditTable = "AppUser";

    // 角色數 — AppUserRole joins on the string key UserId, not pkid.
    // PasswordHash is intentionally never SELECTed: it is backend-only and must not reach the model.
    private const string SelectColumns = """
        SELECT u.pkid AS Pkid, u.UserId, u.UserName, u.IsActive, u.PasswordUpdatedTime,
               (SELECT COUNT(*) FROM dbo.AppUserRole ur WHERE ur.UserId = u.UserId) AS RoleCount
        FROM   dbo.AppUser u
        """;

    public async Task<IReadOnlyList<AppUser>> GetAllAsync(CancellationToken ct = default)
    {
        const string sql = $"{SelectColumns} ORDER BY u.UserId ASC;";

        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<AppUser>(new CommandDefinition(sql, cancellationToken: ct));
        return rows.AsList();
    }

    public async Task<IReadOnlyList<AppUser>> QueryAsync(AppUserQuery query, CancellationToken ct = default)
    {
        // One static statement with null-guarded predicates — no SQL string building.
        const string sql = $"""
            {SelectColumns}
            WHERE  (@Keyword IS NULL
                    OR u.UserId   LIKE @Like ESCAPE '\'
                    OR u.UserName LIKE @Like ESCAPE '\')
              AND  (@IsActive IS NULL OR u.IsActive = @IsActive)
            ORDER  BY u.UserId ASC;
            """;

        var keyword = string.IsNullOrWhiteSpace(query.Keyword) ? null : query.Keyword.Trim();

        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<AppUser>(new CommandDefinition(
            sql,
            new
            {
                Keyword = keyword,
                Like = keyword is null ? null : SqlLike.ToPattern(keyword),
                query.IsActive
            },
            cancellationToken: ct));
        return rows.AsList();
    }

    public async Task<AppUser?> GetByIdAsync(string userId, CancellationToken ct = default)
    {
        const string sql = $"""
            {SelectColumns}
            WHERE u.UserId = @UserId;

            SELECT ur.RoleId
            FROM   dbo.AppUserRole ur
            WHERE  ur.UserId = @UserId
            ORDER  BY ur.RoleId;
            """;

        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var multi = await conn.QueryMultipleAsync(new CommandDefinition(
            sql, new { UserId = userId }, cancellationToken: ct));

        var user = await multi.ReadSingleOrDefaultAsync<AppUser>();
        if (user is null) return null;

        user.RoleIds = (await multi.ReadAsync<string>()).AsList();
        return user;
    }

    public async Task<bool> ExistsAsync(string userId, CancellationToken ct = default)
    {
        const string sql = "SELECT CASE WHEN EXISTS (SELECT 1 FROM dbo.AppUser WHERE UserId = @UserId) THEN 1 ELSE 0 END;";

        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        return await conn.ExecuteScalarAsync<bool>(new CommandDefinition(
            sql, new { UserId = userId }, cancellationToken: ct));
    }

    public async Task<int> InsertAsync(AppUserRequest request, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO dbo.AppUser (UserId, UserName, IsActive, PasswordHash, PasswordUpdatedTime)
            VALUES (@UserId, @UserName, @IsActive, @PasswordHash, @PasswordUpdatedTime);
            SELECT CAST(SCOPE_IDENTITY() AS int);
            """;

        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);

        // The default password lives in SysConfig; hash it once, up front, on this connection.
        var passwordHash = await HashDefaultPasswordAsync(conn, null, ct);

        await using var tx = await conn.BeginTransactionAsync(ct);

        var pkid = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            sql,
            new
            {
                request.UserId,
                request.UserName,
                request.IsActive,
                PasswordHash = passwordHash,
                PasswordUpdatedTime = DateTime.Now
            },
            tx, cancellationToken: ct));

        await SyncUserRolesAsync(conn, tx, request.UserId, request.RoleIds, ct);

        var inserted = await LoadForAuditAsync(conn, tx, request.UserId, ct);
        await auditWriter.LogInsertAsync(conn, tx, AuditTable, inserted!, ct);

        await tx.CommitAsync(ct);
        return pkid;
    }

    public async Task<bool> UpdateAsync(AppUserRequest request, CancellationToken ct = default)
    {
        // UserId is the key and is immutable — never in the SET list. Renaming it would orphan
        // AppUserRole rows (FK_AppUserRole_AppUser has no ON UPDATE CASCADE).
        // PasswordHash / PasswordUpdatedTime are deliberately NOT updated here — reset-password owns them.
        const string sql = """
            UPDATE dbo.AppUser
            SET    UserName = @UserName,
                   IsActive = @IsActive
            WHERE  UserId = @UserId;
            """;

        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        // Load the "before" first so the audit's changed-column list is accurate; a missing user is the
        // 404 case (the old affected == 0 short-circuit).
        var before = await LoadForAuditAsync(conn, tx, request.UserId, ct);
        if (before is null)
        {
            await tx.RollbackAsync(ct);
            return false;
        }

        await conn.ExecuteAsync(new CommandDefinition(
            sql,
            new { request.UserId, request.UserName, request.IsActive },
            tx, cancellationToken: ct));

        await SyncUserRolesAsync(conn, tx, request.UserId, request.RoleIds, ct);

        var after = await LoadForAuditAsync(conn, tx, request.UserId, ct);
        await auditWriter.LogUpdateAsync(conn, tx, AuditTable, before, after!, ct);

        await tx.CommitAsync(ct);
        return true;
    }

    public async Task<bool> DeleteAsync(string userId, CancellationToken ct = default)
    {
        // FK_AppUserRole_AppUser has no ON DELETE CASCADE, so junction rows must go first.
        const string deleteUserRoles = "DELETE FROM dbo.AppUserRole WHERE UserId = @UserId;";
        const string deleteUser = "DELETE FROM dbo.AppUser WHERE UserId = @UserId;";

        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        // Load before deleting so the audit can record the row's first string column (UserId); a
        // missing user is the 404 case.
        var user = await LoadForAuditAsync(conn, tx, userId, ct);
        if (user is null)
        {
            await tx.RollbackAsync(ct);
            return false;
        }

        await conn.ExecuteAsync(new CommandDefinition(
            deleteUserRoles, new { UserId = userId }, tx, cancellationToken: ct));

        await conn.ExecuteAsync(new CommandDefinition(
            deleteUser, new { UserId = userId }, tx, cancellationToken: ct));

        await auditWriter.LogDeleteAsync(conn, tx, AuditTable, user, ct);

        await tx.CommitAsync(ct);
        return true;
    }

    public async Task<bool> ResetPasswordAsync(string userId, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE dbo.AppUser
            SET    PasswordHash = @PasswordHash, PasswordUpdatedTime = @Now
            WHERE  UserId = @UserId;
            """;

        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);

        // Hash before opening the transaction, as InsertAsync does: the KDF costs ~200ms of CPU and
        // holding a write transaction open across it buys nothing.
        var passwordHash = await HashDefaultPasswordAsync(conn, null, ct);

        await using var tx = await conn.BeginTransactionAsync(ct);

        // Load "before" first: it doubles as the existence check (the old affected == 0 short-circuit)
        // and gives the audit an accurate changed-column list.
        var before = await LoadForAuditAsync(conn, tx, userId, ct);
        if (before is null)
        {
            await tx.RollbackAsync(ct);
            return false;
        }

        await conn.ExecuteAsync(new CommandDefinition(
            sql,
            new { UserId = userId, PasswordHash = passwordHash, Now = DateTime.Now },
            tx, cancellationToken: ct));

        // An admin resetting someone else's password is the most sensitive action in this app, so it
        // is the last one that should be invisible — it audits like every other write (CLAUDE.md's
        // Row Audit rule), on the same conn/tx. Mirrors AuthRepository.UpdatePasswordAsync: the entry
        // is an AppUser Update whose changed column is PasswordUpdatedTime. Nothing about the secret
        // reaches the trail — AppUser carries no PasswordHash property (LoadForAuditAsync does not
        // select it), and an Update entry records changed property NAMES, not values. So it says
        // "this account's password was reset, by this admin, at this time", which is the useful part.
        var after = await LoadForAuditAsync(conn, tx, userId, ct);
        await auditWriter.LogUpdateAsync(conn, tx, AuditTable, before, after!, ct);

        await tx.CommitAsync(ct);
        return true;
    }

    /// <summary>
    /// Loads the user's own columns (no derived RoleCount, no RoleIds set, and never PasswordHash) as
    /// the audit before/after snapshot, keyed on the string UserId and on the caller's transaction so it
    /// sees the in-flight change. Pkid is SELECTed for PrimaryKeyValues even though it is not the key.
    /// </summary>
    private static async Task<AppUser?> LoadForAuditAsync(
        DbConnection conn, DbTransaction tx, string userId, CancellationToken ct)
        => await conn.QuerySingleOrDefaultAsync<AppUser>(new CommandDefinition("""
            SELECT pkid AS Pkid, UserId, UserName, IsActive, PasswordUpdatedTime
            FROM   dbo.AppUser
            WHERE  UserId = @UserId;
            """, new { UserId = userId }, tx, cancellationToken: ct));

    /// <summary>N-N sync: delete-then-reinsert, inside the caller's transaction.</summary>
    private static async Task SyncUserRolesAsync(
        DbConnection conn, DbTransaction tx, string userId, List<string>? roleIds, CancellationToken ct)
    {
        await conn.ExecuteAsync(new CommandDefinition(
            "DELETE FROM dbo.AppUserRole WHERE UserId = @UserId;",
            new { UserId = userId }, tx, cancellationToken: ct));

        // Distinct is required: PK_AppUserRole(UserId, RoleId) means a duplicate in the payload
        // would throw 2627 and abort the whole save.
        var ids = (roleIds ?? [])
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => r.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (ids.Count == 0) return;

        await conn.ExecuteAsync(new CommandDefinition(
            "INSERT INTO dbo.AppUserRole (UserId, RoleId) VALUES (@UserId, @RoleId);",
            ids.Select(r => new { UserId = userId, RoleId = r }),
            tx, cancellationToken: ct));
    }

    /// <summary>
    /// Reads the default password out of SysConfig (configKey='appConfig', a JSON object with a
    /// `defaultPassword` property) and hashes it via <see cref="PasswordHasher.Hash"/> — salted
    /// PBKDF2, so two accounts seeded from the same default no longer share a byte-identical hash.
    /// Throws if the config or property is missing — that is a server-configuration fault, not a
    /// client error.
    /// </summary>
    private static async Task<string> HashDefaultPasswordAsync(
        DbConnection conn, DbTransaction? tx, CancellationToken ct)
    {
        const string sql = "SELECT configValue FROM dbo.SysConfig WHERE configKey = 'appConfig';";

        var json = await conn.ExecuteScalarAsync<string?>(new CommandDefinition(
            sql, transaction: tx, cancellationToken: ct));

        if (string.IsNullOrWhiteSpace(json))
            throw new InvalidOperationException("SysConfig 'appConfig' 未設定，無法取得預設密碼。");

        string? defaultPassword;
        try
        {
            using var doc = JsonDocument.Parse(json);
            defaultPassword = doc.RootElement.TryGetProperty("defaultPassword", out var prop)
                ? prop.GetString()
                : null;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("SysConfig 'appConfig' 內容不是有效的 JSON。", ex);
        }

        if (string.IsNullOrEmpty(defaultPassword))
            throw new InvalidOperationException("SysConfig 'appConfig' 缺少 defaultPassword 屬性。");

        return PasswordHasher.Hash(defaultPassword);
    }
}
