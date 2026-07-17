using CMS.API.Models;

namespace CMS.API.Repositories;

public interface IAuthRepository
{
    /// <summary>
    /// Verifies credentials against AppUser: UserId matches, IsActive = 1, and the supplied password
    /// verifies against the stored hash. Returns the user with its role ids on success, or null when
    /// any check fails (wrong password, unknown user, inactive, or blank input) — the caller must not
    /// reveal which. Every failure path costs the same, so timing does not reveal it either.
    ///
    /// Side effect: a successful login against a legacy (pre-2026-07-17, unsalted SHA-256) row
    /// silently re-hashes it to the current format. See <see cref="Infrastructure.PasswordHasher"/>.
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
    /// Audited as an AppUser Update on the same transaction.
    /// </summary>
    Task<bool> UpdateUserNameAsync(string userId, string userName, CancellationToken ct = default);

    /// <summary>
    /// True when <paramref name="password"/> verifies against the stored <c>PasswordHash</c> for
    /// <paramref name="userId"/>. The hash is read into the repository and compared there — a salted
    /// hash cannot be matched in the WHERE — and never leaves it. Blank input fails closed.
    /// </summary>
    Task<bool> VerifyPasswordAsync(string userId, string password, CancellationToken ct = default);

    /// <summary>
    /// Stores a fresh salted hash of <paramref name="newPassword"/> and stamps
    /// <c>PasswordUpdatedTime = now</c> for <paramref name="userId"/>. Returns false when no row
    /// matches. The caller is responsible for having verified the current password and enforced
    /// complexity first. Audited as an AppUser Update (changed column: PasswordUpdatedTime) on the
    /// same transaction — the trail records that the password changed, never anything about it.
    /// </summary>
    Task<bool> UpdatePasswordAsync(string userId, string newPassword, CancellationToken ct = default);
}
