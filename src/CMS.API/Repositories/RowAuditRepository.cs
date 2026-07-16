using CMS.API.Data;
using CMS.API.Models;
using Dapper;

namespace CMS.API.Repositories;

public sealed class RowAuditRepository(IDbConnectionFactory connectionFactory) : IRowAuditRepository
{
    public async Task<IReadOnlyList<RowAuditEntry>> GetForRecordAsync(
        string tableName, string primaryKeyValue, CancellationToken ct = default)
    {
        // Newest first. Tie-break on pkid DESC: DateTime is a wall-clock value (DateTime.Now), so two
        // changes in the same tick would otherwise order arbitrarily — the IDENTITY pkid preserves the
        // true insert order within a tick.
        const string sql = """
            SELECT [DateTime], UserName, ActionType, ActionDesc
            FROM   dbo.RowAudit
            WHERE  TableName = @TableName AND PrimaryKeyValues = @PrimaryKeyValue
            ORDER  BY [DateTime] DESC, pkid DESC;
            """;

        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<RowAuditEntry>(new CommandDefinition(
            sql, new { TableName = tableName, PrimaryKeyValue = primaryKeyValue }, cancellationToken: ct));
        return rows.AsList();
    }
}
