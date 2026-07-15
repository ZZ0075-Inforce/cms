using CMS.API.Infrastructure;
using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace CMS.API.Controllers;

/// <summary>
/// CRUD + slot moves for 首頁上稿作業 FeaturedPromoItem.
/// The resource identity is <c>pkid</c> — a genuine primary key (PK_FeaturedPromoItem over an int
/// IDENTITY), so the route carries an <c>:int</c> constraint. The board queries by the active
/// TrainingCenter tab and the selected Monday–Sunday week.
/// </summary>
[ApiController]
[Route("api/featured-promo-items")]
[Produces("application/json")]
public class FeaturedPromoItemsController(IFeaturedPromoItemRepository repository) : ControllerBase
{
    /// <summary>All rows, ordered by date/centre/slot.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<FeaturedPromoItem>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<FeaturedPromoItem>>> GetAll(CancellationToken ct)
        => Ok(await repository.GetAllAsync(ct));

    /// <summary>Filtered search — the board sends a TrainingCenter pkid and a one-week ScheduleOn range.</summary>
    [HttpPost("query")]
    [ProducesResponseType(typeof(IReadOnlyList<FeaturedPromoItem>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<FeaturedPromoItem>>> Query(
        [FromBody] FeaturedPromoItemQuery query, CancellationToken ct)
        => Ok(await repository.QueryAsync(query, ct));

    /// <summary>A single row by pkid.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(FeaturedPromoItem), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FeaturedPromoItem>> GetById(int id, CancellationToken ct)
    {
        var item = await repository.GetByIdAsync(id, ct);
        return item is null ? NotFound(NotFoundMessage(id)) : Ok(item);
    }

    /// <summary>Creates a row. pkid is DB-generated and any value in the body is ignored.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(FeaturedPromoItem), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<FeaturedPromoItem>> Create(
        [FromBody] FeaturedPromoItemRequest request, CancellationToken ct)
    {
        try
        {
            var pkid = await repository.InsertAsync(request, ct);
            var created = await repository.GetByIdAsync(pkid, ct);
            return CreatedAtAction(nameof(GetById), new { id = pkid }, created);
        }
        // A Promotion / TrainingCenter pkid in the payload that does not exist. Bad input.
        catch (SqlException ex) when (ex.Number == SqlErrorNumbers.ForeignKeyViolation)
        {
            return BadRequest(InvalidReferenceMessage());
        }
        // Another row already holds this (date, centre, slot). The UNIQUE index blocks it.
        catch (SqlException ex) when (SqlErrorNumbers.IsDuplicateKey(ex.Number))
        {
            return Conflict(DuplicateSlotMessage());
        }
    }

    /// <summary>Updates a row. pkid is read from the body, per convention.</summary>
    [HttpPut]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update([FromBody] FeaturedPromoItemRequest request, CancellationToken ct)
    {
        try
        {
            var updated = await repository.UpdateAsync(request, ct);
            return updated ? NoContent() : NotFound(NotFoundMessage(request.Pkid));
        }
        catch (SqlException ex) when (ex.Number == SqlErrorNumbers.ForeignKeyViolation)
        {
            return BadRequest(InvalidReferenceMessage());
        }
        catch (SqlException ex) when (SqlErrorNumbers.IsDuplicateKey(ex.Number))
        {
            return Conflict(DuplicateSlotMessage());
        }
    }

    /// <summary>Deletes a row. FeaturedPromoItem is a leaf — nothing FKs it, so no 409 path.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var deleted = await repository.DeleteAsync(id, ct);
        return deleted ? NoContent() : NotFound(NotFoundMessage(id));
    }

    /// <summary>
    /// Moves a row one slot up or down within its own date/centre, swapping with the target slot's
    /// occupant if there is one. "up" = Slot − 1, "down" = Slot + 1.
    /// </summary>
    [HttpPost("{id:int}/move")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Move(
        int id, [FromBody] FeaturedPromoItemMoveRequest request, CancellationToken ct)
    {
        var delta = request.Direction?.Trim().ToLowerInvariant() switch
        {
            "up" => -1,
            "down" => 1,
            _ => 0
        };

        if (delta == 0)
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "移動方向無效",
                Detail = "方向必須是 up 或 down。"
            });

        var result = await repository.MoveSlotAsync(id, delta, ct);
        return result switch
        {
            SlotMoveResult.Moved => NoContent(),
            SlotMoveResult.NotFound => NotFound(NotFoundMessage(id)),
            _ => BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "無法移動版位",
                Detail = "已到達第一或最後一個版位，無法再移動。"
            })
        };
    }

    private static ProblemDetails InvalidReferenceMessage() => new()
    {
        Status = StatusCodes.Status400BadRequest,
        Title = "關聯資料不存在",
        Detail = "指定的促銷活動或訓練中心不存在，請重新選擇。"
    };

    private static ProblemDetails DuplicateSlotMessage() => new()
    {
        Status = StatusCodes.Status409Conflict,
        Title = "版位已被占用",
        Detail = "同一日期與訓練中心的這個版位已有上稿資料，請改用其他版位。"
    };

    private static ProblemDetails NotFoundMessage(int pkid) => new()
    {
        Status = StatusCodes.Status404NotFound,
        Title = "找不到上稿資料",
        Detail = $"找不到主代碼「{pkid}」的上稿資料。"
    };
}
