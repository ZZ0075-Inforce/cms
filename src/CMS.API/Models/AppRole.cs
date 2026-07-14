namespace CMS.API.Models;

/// <summary>
/// Response model for dbo.AppRole (角色).
/// Note: <see cref="Pkid"/> is an IDENTITY column but is NOT the primary key — RoleId is.
/// It is returned for display (主代碼) only and never addresses a resource.
/// </summary>
public class AppRole
{
    public int Pkid { get; set; }
    public string RoleId { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public int PermissionLevel { get; set; } = 100;
    public string? Description { get; set; }

    /// <summary>使用者數 — subquery count over the AppUserRole junction.</summary>
    public int UserCount { get; set; }

    /// <summary>Assigned users. Populated only on GET by id.</summary>
    public List<string> UserIds { get; set; } = [];
}
