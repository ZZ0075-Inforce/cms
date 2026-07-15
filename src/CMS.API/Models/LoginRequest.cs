namespace CMS.API.Models;

/// <summary>
/// Credentials posted to <c>POST /api/Auth/login</c>. The raw password is only ever hashed and
/// compared server-side — it is never stored, logged, or echoed back.
/// </summary>
public class LoginRequest
{
    public string UserId { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
