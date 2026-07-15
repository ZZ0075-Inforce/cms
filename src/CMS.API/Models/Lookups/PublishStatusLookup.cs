namespace CMS.API.Models.Lookups;

/// <summary>
/// Slim PublishStatus row for dropdowns. PublishStatus is a fixed enum-like table (pkid is a
/// tinyint, NOT an IDENTITY), the FK target of Course.PublishStatus_pkid.
/// </summary>
public class PublishStatusLookup
{
    public byte Pkid { get; set; }
    public string Description { get; set; } = string.Empty;
}
