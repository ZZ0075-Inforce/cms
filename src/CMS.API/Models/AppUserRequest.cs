using System.ComponentModel.DataAnnotations;

namespace CMS.API.Models;

/// <summary>
/// Write DTO for dbo.AppUser. The key (UserId) travels in the body on both POST and PUT,
/// per the coding convention. Pkid is DB-generated and never accepted from the client.
///
/// PasswordHash is NOT here: it is set backend-side from the SysConfig default password on create,
/// and only changed via the dedicated reset-password endpoint. No password ever enters through this DTO.
/// </summary>
public class AppUserRequest
{
    /// <summary>
    /// 使用者代碼 — the primary key. Immutable on update.
    /// The character restriction is load-bearing: UserId becomes a URL path segment, and Kestrel does
    /// not round-trip an encoded '/' (%2F). Unlike AppRole.RoleId's strict alphabet, real user ids are
    /// logins / emails (e.g. miles@uuu.com.tw), so we forbid only whitespace and slashes — enough to
    /// keep every id addressable while allowing '@', '.', etc.
    /// </summary>
    [Required(ErrorMessage = "使用者代碼為必填")]
    [StringLength(200)]
    [RegularExpression(@"^[^\s/\\]+$", ErrorMessage = "使用者代碼不可包含空白或斜線")]
    public string UserId { get; set; } = string.Empty;

    [Required(ErrorMessage = "使用者名稱為必填")]
    [StringLength(200)]
    public string UserName { get; set; } = string.Empty;

    /// <summary>啟用 — DB default is 1 (DF_AppUser_IsActive).</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Assigned roles (N-N via AppUserRole). Deliberate deviation from the convention's
    /// `List&lt;int&gt;` for n-n keys: AppUserRole.RoleId is nvarchar(200). Mirrors AppRole.UserIds.
    /// </summary>
    public List<string> RoleIds { get; set; } = [];
}
