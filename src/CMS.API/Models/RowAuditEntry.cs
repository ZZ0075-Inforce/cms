namespace CMS.API.Models;

/// <summary>
/// One entry of a single record's audit history, as returned to the client. A read-only projection of
/// <see cref="RowAudit"/> that exposes only the four columns the UI shows — never the internal Pkid,
/// TableName or PrimaryKeyValues used to locate the row.
/// </summary>
public sealed class RowAuditEntry
{
    /// <summary>When the change happened (server local time, as written by the audit writer).</summary>
    public DateTime DateTime { get; set; }

    /// <summary>The user who made the change, or "system" for a change with no signed-in user.</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>Insert / Update / Delete.</summary>
    public string ActionType { get; set; } = string.Empty;

    /// <summary>
    /// Insert/Delete: the row's first string column (a Name/Title/Code); Update: the comma-separated
    /// names of the columns that changed. Null is possible for legacy rows.
    /// </summary>
    public string? ActionDesc { get; set; }
}
