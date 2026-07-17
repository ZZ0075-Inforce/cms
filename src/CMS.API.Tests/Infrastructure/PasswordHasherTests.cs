using System.Security.Cryptography;
using System.Text;
using CMS.API.Infrastructure;

namespace CMS.API.Tests.Infrastructure;

/// <summary>
/// Unit tests for <see cref="PasswordHasher"/> (PBKDF2-HMAC-SHA256, salted, with a legacy SHA-256
/// compatibility path). DB-free.
///
/// The legacy tests carry the most weight: every AppUser row written before 2026-07-17 holds a bare
/// SHA-256 hex digest, so a regression in that path locks every existing account out of the system.
/// </summary>
public sealed class PasswordHasherTests
{
    private const string Password = "P@ssw0rd!";

    /// <summary>A digest in exactly the format the pre-2026-07-17 PasswordHasher produced.</summary>
    private static string LegacyHashOf(string password) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(password)));

    // ---------- Round trip ----------

    [Fact]
    public void Verify_AcceptsTheCorrectPassword()
        => Assert.True(PasswordHasher.Verify(Password, PasswordHasher.Hash(Password)));

    [Fact]
    public void Verify_RejectsTheWrongPassword()
        => Assert.False(PasswordHasher.Verify("not-it", PasswordHasher.Hash(Password)));

    [Fact]
    public void Verify_IsCaseSensitive()
        => Assert.False(PasswordHasher.Verify(Password.ToUpperInvariant(), PasswordHasher.Hash(Password)));

    [Fact]
    public void Hash_IsSalted_SoTheSamePasswordNeverProducesTheSameString()
    {
        // The property that stops one dumped column from revealing which accounts share a password.
        Assert.NotEqual(PasswordHasher.Hash(Password), PasswordHasher.Hash(Password));
    }

    [Fact]
    public void Hash_FitsThePasswordHashColumn()
    {
        // AppUser.PasswordHash is nvarchar(800). If this ever exceeds it, saves fail at runtime.
        Assert.True(PasswordHasher.Hash(Password).Length <= 800);
    }

    [Fact]
    public void Hash_IsSelfDescribing_SoTheWorkFactorCanBeRaisedLater()
    {
        // Parameters travel with the hash; a future Iterations bump must not invalidate old rows.
        Assert.StartsWith("pbkdf2-sha256$600000$", PasswordHasher.Hash(Password));
    }

    // ---------- Legacy SHA-256 rows ----------

    [Fact]
    public void Verify_StillAcceptsALegacySha256Row()
    {
        // Existing users must not be locked out by the format change.
        Assert.True(PasswordHasher.Verify(Password, LegacyHashOf(Password)));
    }

    [Fact]
    public void Verify_RejectsTheWrongPasswordAgainstALegacyRow()
        => Assert.False(PasswordHasher.Verify("not-it", LegacyHashOf(Password)));

    [Fact]
    public void NeedsRehash_IsTrue_ForALegacyRow()
        => Assert.True(PasswordHasher.NeedsRehash(LegacyHashOf(Password)));

    [Fact]
    public void NeedsRehash_IsFalse_ForAFreshHash()
        => Assert.False(PasswordHasher.NeedsRehash(PasswordHasher.Hash(Password)));

    [Fact]
    public void NeedsRehash_IsTrue_WhenTheStoredWorkFactorHasFallenBehind()
    {
        // A hash minted at a lower iteration count must be upgraded on next login.
        var stale = PasswordHasher.Hash(Password).Replace("$600000$", "$1000$");
        Assert.True(PasswordHasher.NeedsRehash(stale));
    }

    [Fact]
    public void Verify_HonoursTheIterationCountEncodedInTheHash_NotTheCurrentConstant()
    {
        // Proves old hashes keep working after a future Iterations bump: this hash's own 600000 is
        // what gets used. If Verify read the constant instead, re-encoding the count would break it.
        var hash = PasswordHasher.Hash(Password);
        Assert.Contains("$600000$", hash);
        Assert.True(PasswordHasher.Verify(Password, hash));
    }

    // ---------- Hostile / malformed stored values ----------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("garbage")]
    [InlineData("test-not-a-real-hash")]
    [InlineData("pbkdf2-sha256$notanint$AAAA$AAAA")]
    [InlineData("pbkdf2-sha256$0$AAAA$AAAA")]
    [InlineData("pbkdf2-sha256$600000$!!!not-base64!!!$AAAA")]
    [InlineData("pbkdf2-sha256$600000$AAAA")]
    [InlineData("bcrypt$600000$AAAA$AAAA")]
    public void Verify_ReturnsFalse_NeverThrows_ForAMalformedStoredHash(string? storedHash)
    {
        // A corrupt hash is a failed login, not a 500. Throwing here would turn one bad row into an
        // exception on every attempt against it.
        Assert.False(PasswordHasher.Verify(Password, storedHash));
    }

    [Fact]
    public void Verify_ReturnsFalse_ForAnEmptyPassword()
        => Assert.False(PasswordHasher.Verify("", PasswordHasher.Hash(Password)));

    [Fact]
    public void SimulateVerifyCost_DoesNotThrow_AndAcceptsNothing()
    {
        // It exists only to burn KDF time on the "no such user" path.
        PasswordHasher.SimulateVerifyCost("anything");
    }
}
