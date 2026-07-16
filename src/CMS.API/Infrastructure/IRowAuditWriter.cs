using System.Data.Common;

namespace CMS.API.Infrastructure;

/// <summary>
/// Cross-cutting audit writer. A repository calls one of these AFTER a change succeeds, passing the
/// same connection (and transaction, where it has one) it just ran the change on, so the audit row is
/// committed or rolled back atomically with the change it describes.
///
/// It is entity-agnostic: <c>TableName</c> is supplied by the caller and everything else
/// (<c>PrimaryKeyValues</c>, <c>ActionDesc</c>) is derived from the entity by reflection — see
/// <see cref="RowAuditWriter"/>.
/// </summary>
public interface IRowAuditWriter
{
    /// <summary>Logs an Insert: ActionDesc is the entity's first string property.</summary>
    Task LogInsertAsync(
        DbConnection conn, DbTransaction? tx, string tableName, object entity, CancellationToken ct = default);

    /// <summary>
    /// Logs an Update: ActionDesc is the comma-separated names of the properties that differ between
    /// <paramref name="before"/> and <paramref name="after"/>. When nothing changed, no row is written.
    /// </summary>
    Task LogUpdateAsync(
        DbConnection conn, DbTransaction? tx, string tableName, object before, object after,
        CancellationToken ct = default);

    /// <summary>Logs a Delete: ActionDesc is the (now-removed) entity's first string property.</summary>
    Task LogDeleteAsync(
        DbConnection conn, DbTransaction? tx, string tableName, object entity, CancellationToken ct = default);
}
