using CMS.API.Models.Lookups;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Controllers;

/// <summary>Slim lists for FK / N-N dropdowns.</summary>
[ApiController]
[Route("api/lookups")]
[Produces("application/json")]
public class LookupsController(ILookupRepository repository) : ControllerBase
{
    [HttpGet("app-users")]
    [ProducesResponseType(typeof(IReadOnlyList<AppUserLookup>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AppUserLookup>>> GetAppUsers(CancellationToken ct)
        => Ok(await repository.GetAppUsersAsync(ct));

    [HttpGet("app-roles")]
    [ProducesResponseType(typeof(IReadOnlyList<AppRoleLookup>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AppRoleLookup>>> GetAppRoles(CancellationToken ct)
        => Ok(await repository.GetAppRolesAsync(ct));
}
