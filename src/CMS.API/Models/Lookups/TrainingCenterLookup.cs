namespace CMS.API.Models.Lookups;

/// <summary>
/// Slim TrainingCenter row for the FeaturedPromoItem board's centre tabs. TrainingCenter is the FK
/// target of FeaturedPromoItem.TrainingCenter_pkid; the tab label is <see cref="Name"/>, its value the
/// <see cref="Pkid"/>.
/// </summary>
public class TrainingCenterLookup
{
    public short Pkid { get; set; }
    public string Name { get; set; } = string.Empty;
}
