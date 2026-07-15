namespace CMS.API.Models.Lookups;

/// <summary>
/// Slim CourseGroup row for dropdowns. CourseGroup is the FK target of Course
/// (Course.CourseGroup_pkid), so this endpoint exists ahead of the Course feature.
/// The label column is Description, not Name.
/// </summary>
public class CourseGroupLookup
{
    public short Pkid { get; set; }
    public string Description { get; set; } = string.Empty;
}
