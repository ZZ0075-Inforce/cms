using CMS.API.Infrastructure;
using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace CMS.API.Controllers;

/// <summary>
/// CRUD for 使用者 AppUser.
/// The resource identity is <c>UserId</c> (the table's actual primary key), not <c>pkid</c> —
/// pkid is an IDENTITY column with no PK/UNIQUE constraint, so the DB never promises it is unique.
/// Hence the untyped <c>{id}</c> route segment (no <c>:int</c> constraint).
///
/// PasswordHash is never accepted or returned. It is set from the SysConfig default on create and
/// only changed via <see cref="ResetPassword"/>.
/// </summary>
[ApiController]
[Route("api/app-users")]
[Produces("application/json")]
public class AppUsersController(IAppUserRepository repository) : ControllerBase
{
    /// <summary>All users, ordered by UserId.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<AppUser>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AppUser>>> GetAll(CancellationToken ct)
        => Ok(await repository.GetAllAsync(ct));

    /// <summary>Filtered search.</summary>
    [HttpPost("query")]
    [ProducesResponseType(typeof(IReadOnlyList<AppUser>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AppUser>>> Query(
        [FromBody] AppUserQuery query, CancellationToken ct)
        => Ok(await repository.QueryAsync(query, ct));

    /// <summary>A single user by UserId, including its assigned roles.</summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(AppUser), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AppUser>> GetById(string id, CancellationToken ct)
    {
        var user = await repository.GetByIdAsync(id, ct);
        return user is null ? NotFound(NotFoundMessage(id)) : Ok(user);
    }

    /// <summary>Creates a user. UserId comes from the body; pkid is DB-generated. PasswordHash is set backend-side.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(AppUser), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AppUser>> Create(
        [FromBody] AppUserRequest request, CancellationToken ct)
    {
        if (await repository.ExistsAsync(request.UserId, ct))
            return Conflict(DuplicateMessage(request.UserId));

        try
        {
            await repository.InsertAsync(request, ct);
        }
        // Backstop for the TOCTOU race between ExistsAsync and the INSERT.
        catch (SqlException ex) when (SqlErrorNumbers.IsDuplicateKey(ex.Number))
        {
            return Conflict(DuplicateMessage(request.UserId));
        }
        // A RoleId in the payload that isn't in AppRole.
        catch (SqlException ex) when (ex.Number == SqlErrorNumbers.ForeignKeyViolation)
        {
            return BadRequest(RoleNotFoundProblem());
        }

        var created = await repository.GetByIdAsync(request.UserId, ct);
        return CreatedAtAction(nameof(GetById), new { id = request.UserId }, created);
    }

    /// <summary>Updates a user. UserId is read from the body (per convention) and is immutable. PasswordHash is untouched.</summary>
    [HttpPut]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update([FromBody] AppUserRequest request, CancellationToken ct)
    {
        try
        {
            var updated = await repository.UpdateAsync(request, ct);
            return updated ? NoContent() : NotFound(NotFoundMessage(request.UserId));
        }
        catch (SqlException ex) when (ex.Number == SqlErrorNumbers.ForeignKeyViolation)
        {
            return BadRequest(RoleNotFoundProblem());
        }
    }

    /// <summary>Deletes a user and its role assignments.</summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        var deleted = await repository.DeleteAsync(id, ct);
        return deleted ? NoContent() : NotFound(NotFoundMessage(id));
    }

    /// <summary>
    /// Resets the user's password to the SysConfig default (re-hashed). Accepts no password value —
    /// the default is the single source, matching the create path.
    ///
    /// Restricted to the <c>Admin</c> role: the JWT carries one <c>role</c> claim per AppUserRole.RoleId
    /// (see <see cref="JwtTokenGenerator.RoleClaim"/>), and <c>RoleClaimType</c> is pointed at it, so an
    /// authenticated caller without the <c>Admin</c> RoleId is rejected with 403 Forbidden.
    /// </summary>
    [Authorize(Roles = "Admin")]
    [HttpPost("{id}/reset-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResetPassword(string id, CancellationToken ct)
    {
        var reset = await repository.ResetPasswordAsync(id, ct);
        return reset ? NoContent() : NotFound(NotFoundMessage(id));
    }

    private static ProblemDetails DuplicateMessage(string userId) => new()
    {
        Status = StatusCodes.Status409Conflict,
        Title = "使用者代碼重複",
        Detail = $"使用者代碼「{userId}」已存在。"
    };

    private static ProblemDetails NotFoundMessage(string userId) => new()
    {
        Status = StatusCodes.Status404NotFound,
        Title = "找不到使用者",
        Detail = $"找不到使用者代碼「{userId}」。"
    };

    private static ProblemDetails RoleNotFoundProblem() => new()
    {
        Status = StatusCodes.Status400BadRequest,
        Title = "角色不存在",
        Detail = "指定的角色不存在，請重新選擇。"
    };
}
