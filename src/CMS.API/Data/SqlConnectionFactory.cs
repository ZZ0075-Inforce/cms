using System.Data.Common;
using Microsoft.Data.SqlClient;

namespace CMS.API.Data;

public sealed class SqlConnectionFactory(IConfiguration configuration) : IDbConnectionFactory
{
    private readonly string _connectionString =
        configuration.GetConnectionString("CMS")
        ?? throw new InvalidOperationException("Missing connection string 'CMS'.");

    public async Task<DbConnection> CreateOpenConnectionAsync(CancellationToken ct = default)
    {
        var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        return connection;
    }
}
