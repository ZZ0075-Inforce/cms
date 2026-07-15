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
}
