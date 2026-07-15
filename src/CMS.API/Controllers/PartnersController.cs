using CMS.API.Infrastructure;
using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace CMS.API.Controllers;

/// <summary>
/// CRUD for 合作廠商 Partner.
/// The resource identity is <c>pkid</c> — here it really is the primary key (PK_Partner over a
/// smallint IDENTITY), so the route carries an <c>:int</c> constraint and needs no URL encoding.
/// </summary>
[ApiController]
[Route("api/partners")]
[Produces("application/json")]
public class PartnersController(IPartnerRepository repository) : ControllerBase
{
    /// <summary>All partners, ordered by DisplayOrder.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<Partner>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<Partner>>> GetAll(CancellationToken ct)
        => Ok(await repository.GetAllAsync(ct));

    /// <summary>Filtered search.</summary>
    [HttpPost("query")]
    [ProducesResponseType(typeof(IReadOnlyList<Partner>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<Partner>>> Query(
        [FromBody] PartnerQuery query, CancellationToken ct)
        => Ok(await repository.QueryAsync(query, ct));

    /// <summary>A single partner by pkid.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(Partner), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Partner>> GetById(short id, CancellationToken ct)
    {
        var partner = await repository.GetByIdAsync(id, ct);
        return partner is null ? NotFound(NotFoundMessage(id)) : Ok(partner);
    }

    /// <summary>Creates a partner. pkid is DB-generated and any value in the body is ignored.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(Partner), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Partner>> Create(
        [FromBody] PartnerRequest request, CancellationToken ct)
    {
        // No duplicate-key handling: the DDL puts no UNIQUE constraint on AppKey (or on anything
        // but pkid), so the database never promises it is unique and there is no 409 to raise.
        var pkid = await repository.InsertAsync(request, ct);

        var created = await repository.GetByIdAsync(pkid, ct);
        return CreatedAtAction(nameof(GetById), new { id = pkid }, created);
    }

    /// <summary>Updates a partner. pkid is read from the body, per convention.</summary>
    [HttpPut]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update([FromBody] PartnerRequest request, CancellationToken ct)
    {
        var updated = await repository.UpdateAsync(request, ct);
        return updated ? NoContent() : NotFound(NotFoundMessage(request.Pkid));
    }

    /// <summary>Deletes a partner. Conflicts when courses or certifications still reference it.</summary>
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
        // Course / Certification / PartnerCourseGroup / Promotion2 / Seminar reference Partner and
        // none of those FKs cascade. Deleting a partner still in use is a conflict, not a 500.
        catch (SqlException ex) when (ex.Number == SqlErrorNumbers.ForeignKeyViolation)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "廠商使用中",
                Detail = "這個廠商仍有課程、認證或課程群組使用中，請先移除或改指派後再刪除。"
            });
        }
    }

    private static ProblemDetails NotFoundMessage(short pkid) => new()
    {
        Status = StatusCodes.Status404NotFound,
        Title = "找不到廠商",
        Detail = $"找不到主代碼「{pkid}」的廠商。"
    };
}
