namespace CMS.API.Models;

/// <summary>
/// The signed-in user's profile returned by <c>PUT /api/Auth/profile</c> after a successful update.
/// UserId echoes the authenticated identity (from the JWT); UserName is the newly-saved value.
/// </summary>
public class ProfileResponse
{
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
}
