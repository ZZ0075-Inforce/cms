using CMS.API.Infrastructure;
using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace CMS.API.Controllers;

/// <summary>
/// CRUD for 課程群組 CourseGroup.
/// The resource identity is <c>pkid</c> — a genuine primary key (PK_CourseGroup over a smallint
/// IDENTITY), so the route carries an <c>:int</c> constraint and needs no URL encoding.
/// </summary>
[ApiController]
[Route("api/course-groups")]
[Produces("application/json")]
public class CourseGroupsController(ICourseGroupRepository repository) : ControllerBase
{
    /// <summary>All course groups, newest first.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CourseGroup>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CourseGroup>>> GetAll(CancellationToken ct)
        => Ok(await repository.GetAllAsync(ct));

    /// <summary>Filtered search.</summary>
    [HttpPost("query")]
    [ProducesResponseType(typeof(IReadOnlyList<CourseGroup>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CourseGroup>>> Query(
        [FromBody] CourseGroupQuery query, CancellationToken ct)
        => Ok(await repository.QueryAsync(query, ct));

    /// <summary>A single course group by pkid.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(CourseGroup), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CourseGroup>> GetById(short id, CancellationToken ct)
    {
        var group = await repository.GetByIdAsync(id, ct);
        return group is null ? NotFound(NotFoundMessage(id)) : Ok(group);
    }

    /// <summary>Creates a course group. pkid is DB-generated and any value in the body is ignored.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(CourseGroup), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CourseGroup>> Create(
        [FromBody] CourseGroupRequest request, CancellationToken ct)
    {
        // No duplicate-key handling: the DDL puts no UNIQUE constraint on Description (or on anything
        // but pkid), so the database never promises it is unique and there is no 409 to raise.
        var pkid = await repository.InsertAsync(request, ct);

        var created = await repository.GetByIdAsync(pkid, ct);
        return CreatedAtAction(nameof(GetById), new { id = pkid }, created);
    }

    /// <summary>Updates a course group. pkid is read from the body, per convention.</summary>
    [HttpPut]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update([FromBody] CourseGroupRequest request, CancellationToken ct)
    {
        var updated = await repository.UpdateAsync(request, ct);
        return updated ? NoContent() : NotFound(NotFoundMessage(request.Pkid));
    }

    /// <summary>
    /// Deletes a course group. Conflicts (409) when a PartnerCourseGroup row still references it.
    /// Note: courses in the group are cascade-deleted by the DB and do NOT block this — the front-end
    /// warns about that count before it ever calls here.
    /// </summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(short id, CancellationToken ct)
    {
        try
        {
            var deleted = await repository.DeleteAsync(id, ct);
            return deleted ? NoContent() : NotFound(NotFoundMessage(id));
        }
        // Only FK_PartnerCourseGroup_CourseGroup can raise this — it does not cascade. (The Course FK
        // cascades, so courses never reach here as a conflict.) A group still in use is a 409, not a 500.
        catch (SqlException ex) when (ex.Number == SqlErrorNumbers.ForeignKeyViolation)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "群組使用中",
                Detail = "這個課程群組仍被廠商群組（PartnerCourseGroup）使用中，請先移除相關設定後再刪除。"
            });
        }
    }

    private static ProblemDetails NotFoundMessage(short pkid) => new()
    {
        Status = StatusCodes.Status404NotFound,
        Title = "找不到課程群組",
        Detail = $"找不到主代碼「{pkid}」的課程群組。"
    };
}
