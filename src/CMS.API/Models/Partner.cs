namespace CMS.API.Models;

/// <summary>
/// Response model for dbo.Partner (合作廠商).
/// Unlike AppRole, <see cref="Pkid"/> here IS the primary key — a genuine smallint IDENTITY with
/// PK_Partner on it — so it addresses the resource and the routes carry an :int constraint.
/// </summary>
public class Partner
{
    public short Pkid { get; set; }
    public string Name { get; set; } = string.Empty;
    public string AppKey { get; set; } = string.Empty;
    public string NameOnPartnerMenu { get; set; } = string.Empty;
    public string NameOnCourseDetailPage { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public string? ImageFilename { get; set; }

    /// <summary>對應課程數 — subquery count over Course.Partner_pkid. Display only.</summary>
    public int CourseCount { get; set; }
}
