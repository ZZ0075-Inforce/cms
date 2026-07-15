using System.Text.Json;
using CMS.API.Data;
using CMS.API.Infrastructure;
using CMS.API.Models;
using Dapper;

namespace CMS.API.Repositories;

public sealed class AuthRepository(IDbConnectionFactory connectionFactory) : IAuthRepository
{
    public async Task<AuthenticatedUser?> AuthenticateAsync(
        string userId, string password, CancellationToken ct = default)
    {
        // Blank input can never match a real credential — fail closed before touching the DB
        // (and before hashing a null password).
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrEmpty(password))
            return null;

        // The three checks live in one WHERE so the query yields a row only when *all* pass; the
        // caller gets a single "matched / didn't" answer and cannot tell which check failed.
        // PasswordHash is compared here and never SELECTed — it must not leave the repository.
        const string sql = """
            SELECT u.UserId, u.UserName
            FROM   dbo.AppUser u
            WHERE  u.UserId = @UserId
              AND  u.IsActive = 1
              AND  u.PasswordHash = @PasswordHash;

            SELECT ur.RoleId
            FROM   dbo.AppUserRole ur
            WHERE  ur.UserId = @UserId
            ORDER  BY ur.RoleId;
            """;

        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var multi = await conn.QueryMultipleAsync(new CommandDefinition(
            sql,
            new { UserId = userId, PasswordHash = PasswordHasher.Hash(password) },
            cancellationToken: ct));

        var user = await multi.ReadSingleOrDefaultAsync<AppUser>();
        if (user is null) return null;

        var roleIds = (await multi.ReadAsync<string>()).AsList();
        return new AuthenticatedUser(user.UserId, user.UserName, roleIds);
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
}
