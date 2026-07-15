using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace CMS.API.Infrastructure;

/// <summary>
/// Builds signed HS256 JWT access tokens. A stateless infrastructure helper (like
/// <see cref="PasswordHasher"/>): the signing key is supplied by the caller — read at runtime from
/// SysConfig — never hard-coded here.
///
/// Claim names are the short, unmapped forms (<see cref="JsonWebTokenHandler"/> writes them verbatim,
/// no legacy URI remapping). When JWT bearer auth is later wired up, point
/// <c>TokenValidationParameters.RoleClaimType</c> at <see cref="RoleClaim"/>.
/// </summary>
public static class JwtTokenGenerator
{
    public const string UserIdClaim = "userId";
    public const string UserNameClaim = "userName";
    public const string RoleClaim = "role";

    /// <summary>Every issued token lives for 24 hours from issue.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(24);

    /// <summary>
    /// Signs a token carrying the user's id, name and one <see cref="RoleClaim"/> per role.
    /// </summary>
    /// <param name="signingKey">
    /// The symmetric secret. HS256 requires at least 256 bits (32 UTF-8 bytes); a shorter key throws,
    /// which is a server-configuration fault, not a client error.
    /// </param>
    public static string Generate(
        string signingKey, string userId, string userName, IEnumerable<string> roleIds)
    {
        var claims = new List<Claim>
        {
            new(UserIdClaim, userId),
            new(UserNameClaim, userName)
        };
        claims.AddRange(roleIds.Select(role => new Claim(RoleClaim, role)));

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            SecurityAlgorithms.HmacSha256);

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.Add(Lifetime),
            SigningCredentials = credentials
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}
