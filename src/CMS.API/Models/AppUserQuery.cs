namespace CMS.API.Models;

/// <summary>Search DTO for POST /api/app-users/query. All filters are optional (null = no filter).</summary>
public class AppUserQuery
{
    /// <summary>LIKE across UserId and UserName. PasswordHash is never searched.</summary>
    public string? Keyword { get; set; }

    /// <summary>Tri-state exact match on IsActive: null = 全部, true = 啟用, false = 停用.</summary>
    public bool? IsActive { get; set; }
}
