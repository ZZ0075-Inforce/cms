namespace CMS.API.Models;

/// <summary>
/// One row of the cross-cutting audit trail (dbo.RowAudit). Every successful Insert / Update / Delete
/// on a business table writes exactly one of these. <see cref="Pkid"/> is the table's own IDENTITY and
/// is never supplied on insert.
///
/// <see cref="ActionDesc"/> depends on the verb: for Insert/Delete it is the entity's first string
/// property (a Name/Title/Code); for Update it is the comma-separated list of the property names whose
/// value changed. It is capped at 1000 characters to match the column width.
/// </summary>
public sealed class RowAudit
{
    public int Pkid { get; set; }
    public string TableName { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string PrimaryKeyValues { get; set; } = string.Empty;
    public string ActionType { get; set; } = string.Empty;
    public string? ActionDesc { get; set; }
    public DateTime DateTime { get; set; }
}
