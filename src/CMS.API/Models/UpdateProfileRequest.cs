namespace CMS.API.Models;

/// <summary>
/// Body of <c>PUT /api/Auth/profile</c>. Only <see cref="UserName"/> is modelled — the target UserId is
/// taken from the caller's JWT, never the request, so a user can rename only themselves. Any UserId or
/// role fields a client sends are simply not bound here, and therefore ignored.
/// </summary>
public class UpdateProfileRequest
{
    public string UserName { get; set; } = string.Empty;
}
