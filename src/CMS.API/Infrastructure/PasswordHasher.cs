using System.Security.Cryptography;
using System.Text;

namespace CMS.API.Infrastructure;

/// <summary>
/// SHA-256 password hashing. AppUser.PasswordHash is nvarchar(800); we store the lowercase hex digest
/// (64 chars). There is no login flow yet, so this is only ever used to seed / reset a password to the
/// SysConfig default — the format just needs to be deterministic, not a slow KDF.
/// </summary>
public static class PasswordHasher
{
    public static string Hash(string password)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToHexStringLower(bytes);
    }
}
