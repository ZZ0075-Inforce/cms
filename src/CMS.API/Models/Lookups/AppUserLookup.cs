namespace CMS.API.Models.Lookups;

/// <summary>Slim AppUser row for FK/N-N dropdowns. Frontend renders "UserName (UserId)".</summary>
public class AppUserLookup
{
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}
