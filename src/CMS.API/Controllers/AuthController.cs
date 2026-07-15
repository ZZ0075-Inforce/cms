using System.Security.Claims;
using CMS.API.Infrastructure;
using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Controllers;

/// <summary>
/// Authentication. <c>POST /api/Auth/login</c> checks credentials against AppUser and, on success,
/// issues a 24-hour HS256 JWT carrying the user's id, name and role claims. <c>PUT /api/Auth/profile</c>
/// lets a signed-in user rename themselves.
///
/// A failed login always returns a single generic 401 — the response never reveals whether the
/// UserId was unknown, the password wrong, or the account inactive. PasswordHash is never returned.
///
/// Only <c>login</c> is <see cref="AllowAnonymousAttribute">anonymous</see> — a class-level
/// <c>[AllowAnonymous]</c> would exempt the whole controller and silently make <c>profile</c> public,
/// so anonymity is granted per-action and <c>profile</c> falls under the global RequireAuthenticatedUser
/// policy (made explicit with <c>[Authorize]</c>).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AuthController(IAuthRepository repository) : ControllerBase
{
    /// <summary>Exchanges credentials for a signed access token and the user's profile.</summary>
    [AllowAnonymous] // Logging in cannot itself require a token.
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

    /// <summary>
    /// Updates the signed-in user's UserName. The UserId is taken from the JWT (never the request body),
    /// so a user can rename only themselves — UserId and roles cannot be changed here. UserName is
    /// required (trimmed, non-empty).
    /// </summary>
    [Authorize]
    [HttpPut("profile")]
    [ProducesResponseType(typeof(ProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProfileResponse>> UpdateProfile(
        [FromBody] UpdateProfileRequest request, CancellationToken ct)
    {
        // The identity to update comes from the validated token, never the body. NameClaimType is the
        // userId claim (see ConfigureJwtBearerOptions), so this is the authenticated caller's own id.
        var userId = User.FindFirstValue(JwtTokenGenerator.UserIdClaim);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var userName = request.UserName?.Trim() ?? string.Empty;
        if (userName.Length == 0)
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "更新失敗",
                Detail = "使用者名稱不可為空白。"
            });

        var updated = await repository.UpdateUserNameAsync(userId, userName, ct);
        if (!updated)
            return NotFound();

        return Ok(new ProfileResponse { UserId = userId, UserName = userName });
    }

    /// <summary>
    /// Changes the signed-in user's password. The UserId is taken from the JWT, never the body. In order:
    /// the current password must match the stored hash (else nothing changes), the new password must meet
    /// the complexity policy, and the confirmation must equal the new password. On success the stored hash
    /// and PasswordUpdatedTime are replaced. No password hash ever crosses the API boundary.
    /// </summary>
    [Authorize]
    [HttpPost("change-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordRequest request, CancellationToken ct)
    {
        var userId = User.FindFirstValue(JwtTokenGenerator.UserIdClaim);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        // 1. The current password must be correct — verified against the stored hash. If it is wrong,
        //    return before anything is touched.
        if (!await repository.VerifyPasswordAsync(userId, request.CurrentPassword ?? string.Empty, ct))
            return BadRequest(BadRequestProblem("變更密碼失敗", "目前密碼不正確。"));

        // 2. The new password must satisfy the complexity policy.
        if (!PasswordPolicy.IsComplexEnough(request.NewPassword))
            return BadRequest(BadRequestProblem("變更密碼失敗", PasswordPolicy.ViolationMessage));

        // 3. New password and confirmation must match (ordinal — every character counts).
        if (!string.Equals(request.NewPassword, request.ConfirmPassword, StringComparison.Ordinal))
            return BadRequest(BadRequestProblem("變更密碼失敗", "新密碼與確認密碼不一致。"));

        // 4. Persist the new hash and stamp the update time.
        var updated = await repository.UpdatePasswordAsync(userId, request.NewPassword, ct);
        if (!updated)
            return NotFound();

        return NoContent();
    }

    private static ProblemDetails InvalidCredentials() => new()
    {
        Status = StatusCodes.Status401Unauthorized,
        Title = "登入失敗",
        Detail = "使用者代碼或密碼錯誤。"
    };

    private static ProblemDetails BadRequestProblem(string title, string detail) => new()
    {
        Status = StatusCodes.Status400BadRequest,
        Title = title,
        Detail = detail
    };
}
