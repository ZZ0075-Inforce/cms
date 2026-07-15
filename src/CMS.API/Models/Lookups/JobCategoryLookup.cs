namespace CMS.API.Models.Lookups;

/// <summary>Slim JobCategory row for the Course N-N multi-select. pkid is a smallint.</summary>
public class JobCategoryLookup
{
    public short Pkid { get; set; }
    public string Description { get; set; } = string.Empty;
}
