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
}
