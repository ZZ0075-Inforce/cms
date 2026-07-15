namespace CMS.API.Models;

/// <summary>
/// Response model for dbo.CourseGroup (課程群組).
/// <see cref="Pkid"/> IS the primary key — a genuine smallint IDENTITY with PK_CourseGroup on it —
/// so it addresses the resource and the routes carry an :int constraint.
/// </summary>
public class CourseGroup
{
    public short Pkid { get; set; }
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 對應課程數 — subquery count over Course.CourseGroup_pkid. Display only.
    /// Also the number of courses that FK_Course_CourseGroup's ON DELETE CASCADE would take with it,
    /// which is why the list surfaces it in the delete-confirm warning.
    /// </summary>
    public int CourseCount { get; set; }
}
