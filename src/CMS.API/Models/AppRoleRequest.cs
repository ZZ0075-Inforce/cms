using System.ComponentModel.DataAnnotations;

namespace CMS.API.Models;

/// <summary>
/// Write DTO for dbo.AppRole. The key (RoleId) travels in the body on both POST and PUT,
/// per the coding convention. Pkid is DB-generated and is never accepted from the client.
/// </summary>
public class AppRoleRequest
{
    /// <summary>
    /// 角色代碼 — the primary key. Immutable on update.
    /// The character restriction is load-bearing, not cosmetic: RoleId is used as a URL path
    /// segment, and Kestrel does not round-trip an encoded '/' (%2F) back into a path segment.
    /// Restricting the alphabet at creation means no role can exist that is unaddressable.
    /// </summary>
    [Required(ErrorMessage = "角色代碼為必填")]
    [StringLength(200)]
    [RegularExpression(@"^[A-Za-z0-9_\-]+$", ErrorMessage = "角色代碼僅能包含英數字、底線與減號")]
    public string RoleId { get; set; } = string.Empty;

    [Required(ErrorMessage = "角色名稱為必填")]
    [StringLength(200)]
    public string RoleName { get; set; } = string.Empty;

    [Range(0, 9999)]
    public int PermissionLevel { get; set; } = 100;

    /// <summary>描述 — nullable in the DB, so optional here (the mockup's red asterisk notwithstanding).</summary>
    [StringLength(400)]
    public string? Description { get; set; }

    /// <summary>
    /// Assigned users (N-N via AppUserRole). Deliberate deviation from the convention's
    /// `List&lt;int&gt;` for n-n keys: AppUserRole.UserId is nvarchar(200).
    /// </summary>
    public List<string> UserIds { get; set; } = [];
}
