using System.Data.Common;

namespace CMS.API.Data;

/// <summary>
/// Creates open connections to the CMS database. Returns <see cref="DbConnection"/> rather than
/// <c>IDbConnection</c> so callers get the async surface (OpenAsync, BeginTransactionAsync).
/// </summary>
public interface IDbConnectionFactory
{
    Task<DbConnection> CreateOpenConnectionAsync(CancellationToken ct = default);
}
