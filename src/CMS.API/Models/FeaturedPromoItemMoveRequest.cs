namespace CMS.API.Models;

/// <summary>
/// Body for POST /api/featured-promo-items/{id}/move. <see cref="Direction"/> is "up" (Slot − 1) or
/// "down" (Slot + 1); the board's + / − links map to "down" / "up" respectively.
/// </summary>
public class FeaturedPromoItemMoveRequest
{
    public string Direction { get; set; } = string.Empty;
}
