namespace CMS.API.Models.Lookups;

/// <summary>
/// Slim Partner row for dropdowns. Partner is the FK target of Course, Certification,
/// PartnerCourseGroup, Promotion2 and Seminar, so this endpoint exists ahead of those features.
/// </summary>
public class PartnerLookup
{
    public short Pkid { get; set; }
    public string Name { get; set; } = string.Empty;
}
