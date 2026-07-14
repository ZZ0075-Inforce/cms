namespace CMS.API.Models.Lookups;

/// <summary>Slim AppRole row for dropdowns (AppRole is an FK target for AppUserRole).</summary>
public class AppRoleLookup
{
    public string RoleId { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
}
