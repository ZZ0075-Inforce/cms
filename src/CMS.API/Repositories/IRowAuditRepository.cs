using CMS.API.Models;

namespace CMS.API.Repositories;

/// <summary>
/// Read side of the cross-cutting audit trail (dbo.RowAudit). The write side lives in
/// <see cref="Infrastructure.IRowAuditWriter"/>; this only ever SELECTs.
/// </summary>
public interface IRowAuditRepository
{
    /// <summary>
    /// The audit history of ONE record, newest first. <paramref name="tableName"/> matches the
    /// business table's audit name (e.g. "Course") and <paramref name="primaryKeyValue"/> is that
    /// record's pkid as a string — exactly what the writer stored in PrimaryKeyValues.
    /// </summary>
    Task<IReadOnlyList<RowAuditEntry>> GetForRecordAsync(
        string tableName, string primaryKeyValue, CancellationToken ct = default);
}
