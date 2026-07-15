using System.ComponentModel.DataAnnotations;

namespace CMS.API.Models;

/// <summary>
/// Write DTO for dbo.FeaturedPromoItem. Pkid travels in the body on PUT (per the coding convention)
/// and is ignored on POST, where the DB generates it. Carries only the FK <c>{Fk}Pkid</c> values —
/// never the JOIN-resolved <c>PromoCode</c> / <c>TrainingCenterName</c> labels.
///
/// The UI collects a PromoCode and resolves it to <see cref="PromotionPkid"/> via the
/// <c>/api/lookups/promotions/by-code/{code}</c> endpoint before submitting, so the FK-pkid-only shape
/// still holds here.
/// </summary>
public class FeaturedPromoItemRequest
{
    public int Pkid { get; set; }

    [Required]
    public DateOnly ScheduleOn { get; set; }

    [Required]
    public short TrainingCenterPkid { get; set; }

    /// <summary>Display slot; the mockup uses 1..3, but tinyint width is all the DB enforces.</summary>
    [Range(1, 255)]
    public byte Slot { get; set; }

    [Required]
    public int PromotionPkid { get; set; }

    [Required(ErrorMessage = "標題為必填")]
    [StringLength(100)]
    public string Topic { get; set; } = string.Empty;

    [Required(ErrorMessage = "說明為必填")]
    [StringLength(300)]
    public string Description { get; set; } = string.Empty;
}
