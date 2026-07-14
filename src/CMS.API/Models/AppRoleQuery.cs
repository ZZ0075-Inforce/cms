namespace CMS.API.Models;

/// <summary>Search DTO for POST /api/app-roles/query. All filters are optional (null = no filter).</summary>
public class AppRoleQuery
{
    /// <summary>LIKE across RoleId, RoleName and Description.</summary>
    public string? Keyword { get; set; }

    /// <summary>Exact match on PermissionLevel.</summary>
    public int? PermissionLevel { get; set; }
}
