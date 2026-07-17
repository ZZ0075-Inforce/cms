using System.Security.Cryptography;
using System.Text;

namespace CMS.API.Infrastructure;

/// <summary>
/// Password hashing: PBKDF2-HMAC-SHA256, with a per-user random salt and a work factor.
///
/// Stored format (AppUser.PasswordHash, nvarchar(800) — this needs ~90 chars):
/// <code>pbkdf2-sha256$&lt;iterations&gt;$&lt;base64 salt&gt;$&lt;base64 subkey&gt;</code>
/// The parameters travel WITH the hash rather than being read from a constant, so raising
/// <see cref="Iterations"/> later does not invalidate existing passwords — old hashes keep verifying
/// against the iteration count they were created with, and <see cref="NeedsRehash"/> reports them
/// for upgrade on the next login.
///
/// Legacy: rows written before 2026-07-17 hold a bare unsalted SHA-256 hex digest (64 chars).
/// <see cref="Verify"/> still accepts those so nobody is locked out, and <see cref="NeedsRehash"/>
/// flags them so AuthRepository can quietly re-hash on the next successful login. Once no 64-char hex
/// value remains in AppUser.PasswordHash, the legacy path here can be deleted.
/// </summary>
public static class PasswordHasher
{
    private const string Algorithm = "pbkdf2-sha256";

    /// <summary>OWASP's 2023 floor for PBKDF2-HMAC-SHA256. Encoded per-hash, so it can be raised freely.</summary>
    private const int Iterations = 600_000;

    private const int SaltBytes = 16;   // 128-bit salt
    private const int SubkeyBytes = 32; // 256-bit output, matching the PRF

    private const int LegacySha256HexLength = 64;

    /// <summary>Hashes a password for storage. Salted, so the same password never yields the same string twice.</summary>
    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var subkey = Derive(password, salt, Iterations);
        return $"{Algorithm}${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(subkey)}";
    }

    /// <summary>
    /// Verifies a password against a stored hash of either format. Never throws on a malformed or
    /// truncated stored value — it returns false, because a corrupt hash is a failed login, not a 500.
    /// </summary>
    public static bool Verify(string password, string? storedHash)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrWhiteSpace(storedHash)) return false;

        return IsLegacySha256(storedHash)
            ? VerifyLegacy(password, storedHash)
            : VerifyPbkdf2(password, storedHash);
    }

    /// <summary>
    /// True when the stored hash should be replaced after a successful verify — either it is a legacy
    /// digest, or its encoded work factor has fallen behind <see cref="Iterations"/>.
    /// </summary>
    public static bool NeedsRehash(string? storedHash)
    {
        if (string.IsNullOrWhiteSpace(storedHash)) return true;
        if (IsLegacySha256(storedHash)) return true;

        return !TryParse(storedHash, out var iterations, out _, out _) || iterations < Iterations;
    }

    /// <summary>
    /// Spends the same work a real <see cref="Verify"/> would, and discards the result.
    ///
    /// For callers that must not reveal, by answering quickly, that no such user exists. A real KDF
    /// takes ~200ms; returning early for an unknown user while a known one pays that cost turns login
    /// into a user-enumeration oracle measurable over the network. The old SQL-side hash comparison
    /// had no such gap (every input cost one identical query), so this keeps a property the previous
    /// design got for free rather than trading it away for salting.
    /// </summary>
    public static void SimulateVerifyCost(string password) => Verify(password, DummyHash.Value);

    /// <summary>
    /// A throwaway hash of a random value, built once per process, purely to give
    /// <see cref="SimulateVerifyCost"/> something real to grind against. Nothing can verify against it.
    /// </summary>
    private static readonly Lazy<string> DummyHash =
        new(() => Hash(Guid.NewGuid().ToString()), LazyThreadSafetyMode.ExecutionAndPublication);

    private static byte[] Derive(string password, byte[] salt, int iterations) =>
        Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password), salt, iterations, HashAlgorithmName.SHA256, SubkeyBytes);

    private static bool VerifyPbkdf2(string password, string storedHash)
    {
        if (!TryParse(storedHash, out var iterations, out var salt, out var expected)) return false;

        var actual = Derive(password, salt, iterations);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    private static bool VerifyLegacy(string password, string storedHash)
    {
        var actual = SHA256.HashData(Encoding.UTF8.GetBytes(password));

        byte[] expected;
        try
        {
            expected = Convert.FromHexString(storedHash);
        }
        catch (FormatException)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    /// <summary>A bare 64-char hex digest — the pre-2026-07-17 format. The new format always contains '$'.</summary>
    private static bool IsLegacySha256(string storedHash) =>
        storedHash.Length == LegacySha256HexLength && storedHash.All(Uri.IsHexDigit);

    private static bool TryParse(string storedHash, out int iterations, out byte[] salt, out byte[] subkey)
    {
        iterations = 0;
        salt = [];
        subkey = [];

        var parts = storedHash.Split('$');
        if (parts.Length != 4 || parts[0] != Algorithm) return false;
        if (!int.TryParse(parts[1], out iterations) || iterations <= 0) return false;

        try
        {
            salt = Convert.FromBase64String(parts[2]);
            subkey = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        return salt.Length > 0 && subkey.Length > 0;
    }
}
