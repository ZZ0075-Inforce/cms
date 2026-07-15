namespace CMS.API.Models;

/// <summary>
/// Response model for dbo.FeaturedPromoItem (首頁上稿作業).
/// <see cref="Pkid"/> IS the primary key (PK_FeaturedPromoItem over an int IDENTITY), so the routes
/// carry an :int constraint. <see cref="PromoCode"/> and <see cref="TrainingCenterName"/> are
/// JOIN-resolved display labels (Promotion2.PromoCode / TrainingCenter.Name), not writable — the write
/// DTO carries only <see cref="PromotionPkid"/> / <see cref="TrainingCenterPkid"/>.
///
/// A UNIQUE index (ScheduleOn, TrainingCenter_pkid, Slot) means each date/center has at most one row
/// per slot; a duplicate insert/update trips 2627/2601 → the controller maps it to 409.
/// </summary>
public class FeaturedPromoItem
{
    public int Pkid { get; set; }
    public DateOnly ScheduleOn { get; set; }
    public short TrainingCenterPkid { get; set; }
    public byte Slot { get; set; }
    public int PromotionPkid { get; set; }
    public string Topic { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    /// <summary>JOIN-resolved labels for display. Read-only; never in a write.</summary>
    public string PromoCode { get; set; } = string.Empty;
    public string TrainingCenterName { get; set; } = string.Empty;
}
