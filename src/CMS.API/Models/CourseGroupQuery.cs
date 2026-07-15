namespace CMS.API.Models;

/// <summary>
/// Search DTO for POST /api/course-groups/query. CourseGroup has only the one string column, so a
/// keyword is the only filter there is.
/// </summary>
public class CourseGroupQuery
{
    /// <summary>LIKE across Description.</summary>
    public string? Keyword { get; set; }
}
