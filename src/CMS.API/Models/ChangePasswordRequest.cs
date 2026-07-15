namespace CMS.API.Models;

/// <summary>
/// Body of <c>POST /api/Auth/change-password</c>. The target UserId is taken from the JWT, never the
/// request, so a user can only ever change their own password. Raw passwords are hashed and compared
/// server-side — no password hash is ever accepted from, or returned to, the client.
/// </summary>
public class ChangePasswordRequest
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
}
