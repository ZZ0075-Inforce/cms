using CMS.API.Infrastructure;
using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace CMS.API.Controllers;

/// <summary>
/// CRUD for 上架狀態 PublishStatus.
/// The resource identity is <c>pkid</c> — here it is the primary key (PK_PublishingStatus over a
/// tinyint), so the route carries an <c>:int</c> constraint. Unlike Partner, the pkid is NOT an
/// IDENTITY: the client supplies it on create, so a duplicate is a real 409.
/// </summary>
[ApiController]
[Route("api/publish-statuses")]
[Produces("application/json")]
public class PublishStatusesController(IPublishStatusRepository repository) : ControllerBase
{
    /// <summary>All publish statuses, ordered by pkid.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PublishStatus>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PublishStatus>>> GetAll(CancellationToken ct)
        => Ok(await repository.GetAllAsync(ct));

    /// <summary>Filtered search.</summary>
    [HttpPost("query")]
    [ProducesResponseType(typeof(IReadOnlyList<PublishStatus>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PublishStatus>>> Query(
        [FromBody] PublishStatusQuery query, CancellationToken ct)
        => Ok(await repository.QueryAsync(query, ct));

    /// <summary>A single publish status by pkid.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(PublishStatus), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PublishStatus>> GetById(byte id, CancellationToken ct)
    {
        var status = await repository.GetByIdAsync(id, ct);
        return status is null ? NotFound(NotFoundMessage(id)) : Ok(status);
    }

    /// <summary>Creates a publish status. The pkid is client-supplied and must be unique.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(PublishStatus), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PublishStatus>> Create(
        [FromBody] PublishStatusRequest request, CancellationToken ct)
    {
        // pkid is the client-supplied PK (tinyint, NOT IDENTITY), so a clash is possible — pre-check,
        // then re-catch the 2627 as a backstop for the TOCTOU race between ExistsAsync and the INSERT.
        if (await repository.ExistsAsync(request.Pkid, ct))
            return Conflict(DuplicateMessage(request.Pkid));

        try
        {
            await repository.InsertAsync(request, ct);
        }
        catch (SqlException ex) when (SqlErrorNumbers.IsDuplicateKey(ex.Number))
        {
            return Conflict(DuplicateMessage(request.Pkid));
        }

        var created = await repository.GetByIdAsync(request.Pkid, ct);
        return CreatedAtAction(nameof(GetById), new { id = request.Pkid }, created);
    }

    /// <summary>Updates a publish status. pkid is read from the body (per convention) and is immutable.</summary>
    [HttpPut]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update([FromBody] PublishStatusRequest request, CancellationToken ct)
    {
        var updated = await repository.UpdateAsync(request, ct);
        return updated ? NoContent() : NotFound(NotFoundMessage(request.Pkid));
    }

    /// <summary>Deletes a publish status. Conflicts when courses still reference it.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(byte id, CancellationToken ct)
    {
        try
        {
            var deleted = await repository.DeleteAsync(id, ct);
            return deleted ? NoContent() : NotFound(NotFoundMessage(id));
        }
        // FK_Course_PublishStatus does not cascade. Deleting a status still in use is a conflict, not 500.
        catch (SqlException ex) when (ex.Number == SqlErrorNumbers.ForeignKeyViolation)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "上架狀態使用中",
                Detail = "這個上架狀態仍有課程使用中，請先改指派後再刪除。"
            });
        }
    }

    private static ProblemDetails DuplicateMessage(byte pkid) => new()
    {
        Status = StatusCodes.Status409Conflict,
        Title = "主代碼重複",
        Detail = $"主代碼「{pkid}」已存在，請改用其他代碼。"
    };

    private static ProblemDetails NotFoundMessage(byte pkid) => new()
    {
        Status = StatusCodes.Status404NotFound,
        Title = "找不到上架狀態",
        Detail = $"找不到主代碼「{pkid}」的上架狀態。"
    };
}
