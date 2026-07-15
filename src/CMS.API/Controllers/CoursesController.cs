using CMS.API.Infrastructure;
using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace CMS.API.Controllers;

/// <summary>
/// CRUD for 課程 Course.
/// The resource identity is <c>pkid</c> — a genuine primary key (PK_Course over an int IDENTITY), so
/// the route carries an <c>:int</c> constraint and needs no URL encoding.
/// </summary>
[ApiController]
[Route("api/courses")]
[Produces("application/json")]
public class CoursesController(ICourseRepository repository) : ControllerBase
{
    /// <summary>All courses, ordered by DisplayOrder.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<Course>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<Course>>> GetAll(CancellationToken ct)
        => Ok(await repository.GetAllAsync(ct));

    /// <summary>Filtered search.</summary>
    [HttpPost("query")]
    [ProducesResponseType(typeof(IReadOnlyList<Course>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<Course>>> Query(
        [FromBody] CourseQuery query, CancellationToken ct)
        => Ok(await repository.QueryAsync(query, ct));

    /// <summary>A single course by pkid, including its Certification / JobCategory sets.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(Course), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Course>> GetById(int id, CancellationToken ct)
    {
        var course = await repository.GetByIdAsync(id, ct);
        return course is null ? NotFound(NotFoundMessage(id)) : Ok(course);
    }

    /// <summary>Creates a course. pkid is DB-generated and any value in the body is ignored.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(Course), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Course>> Create(
        [FromBody] CourseRequest request, CancellationToken ct)
    {
        try
        {
            var pkid = await repository.InsertAsync(request, ct);
            var created = await repository.GetByIdAsync(pkid, ct);
            return CreatedAtAction(nameof(GetById), new { id = pkid }, created);
        }
        // A Partner / CourseGroup / PublishStatus / Certification / JobCategory pkid in the payload
        // that does not exist. Bad input, not a server error.
        catch (SqlException ex) when (ex.Number == SqlErrorNumbers.ForeignKeyViolation)
        {
            return BadRequest(InvalidReferenceMessage());
        }
    }

    /// <summary>Updates a course. pkid is read from the body, per convention.</summary>
    [HttpPut]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update([FromBody] CourseRequest request, CancellationToken ct)
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
    }

    /// <summary>
    /// Deletes a course. Conflicts (409) when a CourseFAQ / CourseRelatedLink / HotCourse row still
    /// references it — those FKs do not cascade. (The two N-N junctions cascade and never block.)
    /// </summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        try
        {
            var deleted = await repository.DeleteAsync(id, ct);
            return deleted ? NoContent() : NotFound(NotFoundMessage(id));
        }
        catch (SqlException ex) when (ex.Number == SqlErrorNumbers.ForeignKeyViolation)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "課程使用中",
                Detail = "這門課程仍有課程問答、相關連結或熱門課程設定使用中，請先移除後再刪除。"
            });
        }
    }

    private static ProblemDetails InvalidReferenceMessage() => new()
    {
        Status = StatusCodes.Status400BadRequest,
        Title = "關聯資料不存在",
        Detail = "指定的原廠、課程群組、上架狀態、認證或職務類別不存在，請重新選擇。"
    };

    private static ProblemDetails NotFoundMessage(int pkid) => new()
    {
        Status = StatusCodes.Status404NotFound,
        Title = "找不到課程",
        Detail = $"找不到主代碼「{pkid}」的課程。"
    };
}
