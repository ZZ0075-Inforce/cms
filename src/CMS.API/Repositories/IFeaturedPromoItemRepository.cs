using CMS.API.Models;

namespace CMS.API.Repositories;

/// <summary>Outcome of a slot move so the controller can pick the right status code.</summary>
public enum SlotMoveResult
{
    /// <summary>No row with that pkid.</summary>
    NotFound,

    /// <summary>The move would push the slot below 1 or past 3 — nothing changed.</summary>
    OutOfRange,

    /// <summary>The row moved (swapping with the occupant of the target slot, if any).</summary>
    Moved,

    /// <summary>
    /// The move collided with the UNIQUE (ScheduleOn, TrainingCenter, Slot) index and was rolled back.
    /// A 409, not a 500: the data is intact and retrying is a reasonable thing for the caller to do.
    /// </summary>
    Conflict
}

public interface IFeaturedPromoItemRepository
{
    Task<IReadOnlyList<FeaturedPromoItem>> GetAllAsync(CancellationToken ct = default);

    Task<IReadOnlyList<FeaturedPromoItem>> QueryAsync(
        FeaturedPromoItemQuery query, CancellationToken ct = default);

    Task<FeaturedPromoItem?> GetByIdAsync(int pkid, CancellationToken ct = default);

    Task<int> InsertAsync(FeaturedPromoItemRequest request, CancellationToken ct = default);

    Task<bool> UpdateAsync(FeaturedPromoItemRequest request, CancellationToken ct = default);

    Task<bool> DeleteAsync(int pkid, CancellationToken ct = default);

    /// <summary>
    /// Moves a row one slot up (<paramref name="delta"/> = -1) or down (+1) within its own
    /// date/centre, swapping with whatever occupies the target slot. Runs in a transaction and uses a
    /// temporary slot so the UNIQUE (ScheduleOn, TrainingCenter_pkid, Slot) index never trips mid-swap.
    /// </summary>
    Task<SlotMoveResult> MoveSlotAsync(int pkid, int delta, CancellationToken ct = default);
}
