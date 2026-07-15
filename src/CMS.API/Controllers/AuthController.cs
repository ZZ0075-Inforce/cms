using CMS.API.Infrastructure;
using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Controllers;

/// <summary>
/// Authentication. <c>POST /api/Auth/login</c> checks credentials against AppUser and, on success,
/// issues a 24-hour HS256 JWT carrying the user's id, name and role claims.
///
/// A failed check always returns a single generic 401 — the response never reveals whether the
/// UserId was unknown, the password wrong, or the account inactive. PasswordHash is never returned.
/// </summary>
[ApiController]
[AllowAnonymous] // The one public controller: logging in cannot itself require a token.
[Route("api/[controller]")]
[Produces("application/json")]
public class AuthController(IAuthRepository repository) : ControllerBase
{
    /// <summary>Exchanges credentials for a signed access token and the user's profile.</summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> Login(
        [FromBody] LoginRequest request, CancellationToken ct)
    {
        var user = await repository.AuthenticateAsync(request.UserId, request.Password, ct);
        if (user is null)
            return Unauthorized(InvalidCredentials());

        // Signing secret is read at runtime from SysConfig — only after the credentials pass.
        var signingKey = await repository.GetSigningKeyAsync(ct);
        var token = JwtTokenGenerator.Generate(signingKey, user.UserId, user.UserName, user.RoleIds);

        return Ok(new LoginResponse
        {
            UserId = user.UserId,
            UserName = user.UserName,
            AccessToken = token
        });
    }

    private static ProblemDetails InvalidCredentials() => new()
    {
        Status = StatusCodes.Status401Unauthorized,
        Title = "登入失敗",
        Detail = "使用者代碼或密碼錯誤。"
    };
}
