namespace CMS.API.Models;

/// <summary>
/// Search DTO for POST /api/partners/query. Partner has no FK, bit or date columns, so a keyword
/// is the only filter there is.
/// </summary>
public class PartnerQuery
{
    /// <summary>LIKE across Name, AppKey, NameOnPartnerMenu and NameOnCourseDetailPage.</summary>
    public string? Keyword { get; set; }
}
