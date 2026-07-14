using CMS.API.Infrastructure;
using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace CMS.API.Controllers;

/// <summary>
/// CRUD for 角色 AppRole.
/// The resource identity is <c>RoleId</c> (the table's actual primary key), not <c>pkid</c> —
/// pkid is an IDENTITY column with no PK/UNIQUE constraint, so the DB never promises it is unique.
/// Hence the untyped <c>{id}</c> route segment (no <c>:int</c> constraint).
/// </summary>
[ApiController]
[Route("api/app-roles")]
[Produces("application/json")]
public class AppRolesController(IAppRoleRepository repository) : ControllerBase
{
    /// <summary>All roles, ordered by RoleId.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<AppRole>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AppRole>>> GetAll(CancellationToken ct)
        => Ok(await repository.GetAllAsync(ct));

    /// <summary>Filtered search.</summary>
    [HttpPost("query")]
    [ProducesResponseType(typeof(IReadOnlyList<AppRole>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AppRole>>> Query(
        [FromBody] AppRoleQuery query, CancellationToken ct)
        => Ok(await repository.QueryAsync(query, ct));

    /// <summary>A single role by RoleId, including its assigned users.</summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(AppRole), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AppRole>> GetById(string id, CancellationToken ct)
    {
        var role = await repository.GetByIdAsync(id, ct);
        return role is null ? NotFound(NotFoundMessage(id)) : Ok(role);
    }

    /// <summary>Creates a role. RoleId comes from the body; pkid is DB-generated.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(AppRole), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AppRole>> Create(
        [FromBody] AppRoleRequest request, CancellationToken ct)
    {
        if (await repository.ExistsAsync(request.RoleId, ct))
            return Conflict(DuplicateMessage(request.RoleId));

        try
        {
            await repository.InsertAsync(request, ct);
        }
        // Backstop for the TOCTOU race between ExistsAsync and the INSERT.
        catch (SqlException ex) when (SqlErrorNumbers.IsDuplicateKey(ex.Number))
        {
            return Conflict(DuplicateMessage(request.RoleId));
        }
        // A UserId in the payload that isn't in AppUser.
        catch (SqlException ex) when (ex.Number == SqlErrorNumbers.ForeignKeyViolation)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "使用者不存在",
                Detail = "指定的使用者不存在，請重新選擇。"
            });
        }

        var created = await repository.GetByIdAsync(request.RoleId, ct);
        return CreatedAtAction(nameof(GetById), new { id = request.RoleId }, created);
    }

    /// <summary>Updates a role. RoleId is read from the body (per convention) and is immutable.</summary>
    [HttpPut]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update([FromBody] AppRoleRequest request, CancellationToken ct)
    {
        try
        {
            var updated = await repository.UpdateAsync(request, ct);
            return updated ? NoContent() : NotFound(NotFoundMessage(request.RoleId));
        }
        catch (SqlException ex) when (ex.Number == SqlErrorNumbers.ForeignKeyViolation)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "使用者不存在",
                Detail = "指定的使用者不存在，請重新選擇。"
            });
        }
    }

    /// <summary>Deletes a role and its user assignments.</summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        var deleted = await repository.DeleteAsync(id, ct);
        return deleted ? NoContent() : NotFound(NotFoundMessage(id));
    }

    private static ProblemDetails DuplicateMessage(string roleId) => new()
    {
        Status = StatusCodes.Status409Conflict,
        Title = "角色代碼重複",
        Detail = $"角色代碼「{roleId}」已存在。"
    };

    private static ProblemDetails NotFoundMessage(string roleId) => new()
    {
        Status = StatusCodes.Status404NotFound,
        Title = "找不到角色",
        Detail = $"找不到角色代碼「{roleId}」。"
    };
}
