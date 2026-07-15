using CMS.API.Models;

namespace CMS.API.Repositories;

public interface IAuthRepository
{
    /// <summary>
    /// Verifies credentials against AppUser: UserId matches, IsActive = 1, and PasswordHash equals
    /// SHA-256 of the supplied password. Returns the user with its role ids on success, or null when
    /// any check fails (wrong password, unknown user, inactive, or blank input) — the caller must not
    /// reveal which.
    /// </summary>
    Task<AuthenticatedUser?> AuthenticateAsync(string userId, string password, CancellationToken ct = default);

    /// <summary>
    /// Reads the JWT signing secret (the <c>symmetricSecurityKey</c> property of the SysConfig
    /// 'appConfig' JSON). Throws if the config or property is missing — a server-configuration fault.
    /// </summary>
    Task<string> GetSigningKeyAsync(CancellationToken ct = default);

    /// <summary>
    /// Updates only <c>AppUser.UserName</c> for the given <paramref name="userId"/> (the authenticated
    /// user). The UserId key is never in the SET list; PasswordHash and roles are untouched. Returns
    /// false when no row matches (e.g. the account was deleted after the token was issued).
    /// </summary>
    Task<bool> UpdateUserNameAsync(string userId, string userName, CancellationToken ct = default);
}
