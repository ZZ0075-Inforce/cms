namespace CMS.API.Models;

/// <summary>
/// The profile returned on a successful login. There is deliberately no PasswordHash member —
/// the hash is backend-only and must never cross the API boundary (see spec/gotchas.md).
/// </summary>
public class LoginResponse
{
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;

    /// <summary>The signed JWT access token (HS256), valid for 24 hours from issue.</summary>
    public string AccessToken { get; set; } = string.Empty;
}
