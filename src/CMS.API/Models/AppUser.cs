namespace CMS.API.Models;

/// <summary>
/// Response model for dbo.AppUser (使用者).
/// Note: <see cref="Pkid"/> is an IDENTITY column but is NOT the primary key — UserId is.
/// It is returned for display (主代碼) only and never addresses a resource.
///
/// PasswordHash is deliberately absent: it is backend-only and never crosses the API boundary.
/// </summary>
public class AppUser
{
    public int Pkid { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    /// <summary>密碼更新時間 — set on create / reset-password only. Read-only.</summary>
    public DateTime? PasswordUpdatedTime { get; set; }

    /// <summary>角色數 — subquery count over the AppUserRole junction.</summary>
    public int RoleCount { get; set; }

    /// <summary>Assigned roles. Populated only on GET by id.</summary>
    public List<string> RoleIds { get; set; } = [];
}
