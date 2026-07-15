namespace CMS.API.Models.Lookups;

/// <summary>
/// Slim Promotion2 row for the FeaturedPromoItem edit form's PromoCode lookup. The form takes a
/// PromoCode (unique via IX_Promotion2_UniquePromoCode), resolves it to <see cref="Pkid"/> — the value
/// stored in FeaturedPromoItem.Promotion_pkid — and can prefill <see cref="Topic"/> /
/// <see cref="Description"/> from the promotion.
/// </summary>
public class PromotionLookup
{
    public int Pkid { get; set; }
    public string PromoCode { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
