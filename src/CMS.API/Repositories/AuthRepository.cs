using System.Data.Common;
using System.Text.Json;
using CMS.API.Data;
using CMS.API.Infrastructure;
using CMS.API.Models;
using Dapper;

namespace CMS.API.Repositories;

public sealed class AuthRepository(IDbConnectionFactory connectionFactory, IRowAuditWriter auditWriter)
    : IAuthRepository
{
    /// <summary>Self-service writes here land on dbo.AppUser, so they audit under that table name.</summary>
    private const string AuditTable = "AppUser";

    public async Task<AuthenticatedUser?> AuthenticateAsync(
        string userId, string password, CancellationToken ct = default)
    {
        // Blank input can never match a real credential — fail closed before touching the DB
        // (and before hashing a null password).
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrEmpty(password))
            return null;

        // PasswordHash is SELECTed and compared in code, not in the WHERE. That is forced by salting:
        // every row's hash has its own salt, so there is no value the caller could compute up front to
        // match against. It stays inside this repository — it is read into a private type, never into
        // AppUser, and never reaches a response.
        //
        // The old query folded userId / IsActive / hash into one WHERE so a caller could not tell
        // which check failed. That property is preserved below by giving every failure path the same
        // observable behaviour: same null return, same KDF cost.
        const string sql = """
            SELECT u.UserId, u.UserName, u.PasswordHash
            FROM   dbo.AppUser u
            WHERE  u.UserId = @UserId
              AND  u.IsActive = 1;

            SELECT ur.RoleId
            FROM   dbo.AppUserRole ur
            WHERE  ur.UserId = @UserId
            ORDER  BY ur.RoleId;
            """;

        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var multi = await conn.QueryMultipleAsync(new CommandDefinition(
            sql, new { UserId = userId }, cancellationToken: ct));

        // Both grids are consumed before any decision — bailing between them would leave the reader
        // half-read, and the timing of "did we read roles?" is one more thing not worth leaking.
        var credential = await multi.ReadSingleOrDefaultAsync<UserCredential>();
        var roleIds = (await multi.ReadAsync<string>()).AsList();

        if (credential is null)
        {
            // No such user, or inactive. Pay the KDF cost anyway — see SimulateVerifyCost.
            PasswordHasher.SimulateVerifyCost(password);
            return null;
        }

        if (!PasswordHasher.Verify(password, credential.PasswordHash)) return null;

        // Successful login is the only moment we hold the plaintext AND know the stored hash is stale,
        // so it is the only place a legacy SHA-256 row can be upgraded without forcing a reset.
        if (PasswordHasher.NeedsRehash(credential.PasswordHash))
            await UpgradeHashAsync(conn, userId, password, ct);

        return new AuthenticatedUser(credential.UserId, credential.UserName, roleIds);
    }

    /// <summary>
    /// Re-hashes a verified password in place. PasswordUpdatedTime is deliberately NOT touched: the
    /// user did not change their password, so the "last changed" date they see must not jump — this is
    /// a storage-format upgrade, not a credential change.
    /// </summary>
    private static async Task UpgradeHashAsync(
        DbConnection conn, string userId, string password, CancellationToken ct)
    {
        const string sql = """
            UPDATE dbo.AppUser
            SET    PasswordHash = @PasswordHash
            WHERE  UserId = @UserId;
            """;

        await conn.ExecuteAsync(new CommandDefinition(
            sql,
            new { UserId = userId, PasswordHash = PasswordHasher.Hash(password) },
            cancellationToken: ct));
    }

    /// <summary>
    /// AppUser + its hash, for the credential check only. Deliberately private and separate from
    /// <see cref="AppUser"/>: the hash must not gain a route out of this class by riding on the model.
    /// </summary>
    private sealed class UserCredential
    {
        public string UserId { get; init; } = string.Empty;
        public string UserName { get; init; } = string.Empty;
        public string? PasswordHash { get; init; }
    }

    public async Task<string> GetSigningKeyAsync(CancellationToken ct = default)
    {
        const string sql = "SELECT configValue FROM dbo.SysConfig WHERE configKey = 'appConfig';";

        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        var json = await conn.ExecuteScalarAsync<string?>(new CommandDefinition(sql, cancellationToken: ct));

        if (string.IsNullOrWhiteSpace(json))
            throw new InvalidOperationException("SysConfig 'appConfig' 未設定，無法取得 JWT 簽章金鑰。");

        string? key;
        try
        {
            using var doc = JsonDocument.Parse(json);
            key = doc.RootElement.TryGetProperty("symmetricSecurityKey", out var prop)
                ? prop.GetString()
                : null;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("SysConfig 'appConfig' 內容不是有效的 JSON。", ex);
        }

        if (string.IsNullOrEmpty(key))
            throw new InvalidOperationException("SysConfig 'appConfig' 缺少 symmetricSecurityKey 屬性。");

        return key;
    }

    public async Task<bool> UpdateUserNameAsync(
        string userId, string userName, CancellationToken ct = default)
    {
        // UserId is the immutable key — only UserName is in the SET list. PasswordHash / roles are
        // never touched here. The caller passes the userId from the JWT, so a user renames only itself.
        const string sql = """
            UPDATE dbo.AppUser
            SET    UserName = @UserName
            WHERE  UserId = @UserId;
            """;

        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
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
            sql, new { UserId = userId, UserName = userName }, tx, cancellationToken: ct));

        var after = await LoadForAuditAsync(conn, tx, userId, ct);
        await auditWriter.LogUpdateAsync(conn, tx, AuditTable, before, after!, ct);

        await tx.CommitAsync(ct);
        return true;
    }

    public async Task<bool> VerifyPasswordAsync(
        string userId, string password, CancellationToken ct = default)
    {
        // Fail closed before touching the DB (and before hashing a null password).
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrEmpty(password))
            return false;

        // Read-then-verify rather than comparing in the WHERE: a salted hash has no precomputable
        // value to match on. The hash is read into a local and never leaves this method.
        const string sql = "SELECT PasswordHash FROM dbo.AppUser WHERE UserId = @UserId;";

        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        var storedHash = await conn.ExecuteScalarAsync<string?>(new CommandDefinition(
            sql, new { UserId = userId }, cancellationToken: ct));

        if (storedHash is null)
        {
            // The caller passes a userId from the JWT, so a miss here is near-impossible — but keep the
            // cost identical to a hit regardless, so this never becomes a probe.
            PasswordHasher.SimulateVerifyCost(password);
            return false;
        }

        // No rehash here: the only caller is change-password, which overwrites the hash moments later.
        return PasswordHasher.Verify(password, storedHash);
    }

    public async Task<bool> UpdatePasswordAsync(
        string userId, string newPassword, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE dbo.AppUser
            SET    PasswordHash = @PasswordHash, PasswordUpdatedTime = @Now
            WHERE  UserId = @UserId;
            """;

        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        var before = await LoadForAuditAsync(conn, tx, userId, ct);
        if (before is null)
        {
            await tx.RollbackAsync(ct);
            return false;
        }

        await conn.ExecuteAsync(new CommandDefinition(
            sql,
            new { UserId = userId, PasswordHash = PasswordHasher.Hash(newPassword), Now = DateTime.Now },
            tx, cancellationToken: ct));

        // Audits as an AppUser Update whose changed column is PasswordUpdatedTime. Nothing about the
        // secret reaches the trail: AppUser carries no PasswordHash property, and an Update entry
        // records changed property NAMES rather than values — so this says "this account's password
        // changed, by this user, at this time" and nothing more, which is exactly the useful part.
        var after = await LoadForAuditAsync(conn, tx, userId, ct);
        await auditWriter.LogUpdateAsync(conn, tx, AuditTable, before, after!, ct);

        await tx.CommitAsync(ct);
        return true;
    }

    /// <summary>
    /// The AppUser row as the audit before/after snapshot, on the caller's transaction so it sees the
    /// in-flight change. Mirrors AppUserRepository.LoadForAuditAsync — notably PasswordHash is NOT
    /// selected, which is what keeps it out of the audit trail.
    /// </summary>
    private static async Task<AppUser?> LoadForAuditAsync(
        DbConnection conn, DbTransaction tx, string userId, CancellationToken ct)
        => await conn.QuerySingleOrDefaultAsync<AppUser>(new CommandDefinition("""
            SELECT pkid AS Pkid, UserId, UserName, IsActive, PasswordUpdatedTime
            FROM   dbo.AppUser
            WHERE  UserId = @UserId;
            """, new { UserId = userId }, tx, cancellationToken: ct));
}
