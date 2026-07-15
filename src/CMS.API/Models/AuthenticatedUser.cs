namespace CMS.API.Models;

/// <summary>
/// The identity resolved by a successful credential check: who the user is and which roles they hold.
/// This is the repository's login result — PasswordHash is compared inside the SQL and never leaves it.
/// </summary>
public sealed record AuthenticatedUser(string UserId, string UserName, IReadOnlyList<string> RoleIds);
