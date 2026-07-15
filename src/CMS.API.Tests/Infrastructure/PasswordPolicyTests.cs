using CMS.API.Infrastructure;

namespace CMS.API.Tests.Infrastructure;

/// <summary>
/// The complexity rule in isolation: length >= 8 AND at least 3 of the 4 classes (upper/lower/digit/
/// symbol). This is the source of truth the change-password endpoint and the Angular form both enforce.
/// </summary>
public class PasswordPolicyTests
{
    [Theory]
    // Length >= 8 and 3+ classes → accepted.
    [InlineData("Abcdef12")]     // upper + lower + digit
    [InlineData("abcdef1!")]     // lower + digit + symbol
    [InlineData("ABCDEF1!")]     // upper + digit + symbol
    [InlineData("Abcdefg!")]     // upper + lower + symbol
    [InlineData("Abcd12!@")]     // all four
    public void IsComplexEnough_True_ForStrongPasswords(string password) =>
        Assert.True(PasswordPolicy.IsComplexEnough(password));

    [Theory]
    // Too short (even though it has 3–4 classes).
    [InlineData("Ab1!")]
    [InlineData("Abc123")]
    // Long enough but only two classes.
    [InlineData("abcdefgh")]         // lower only
    [InlineData("abcdefgh12345")]    // lower + digit only
    [InlineData("ABCDEFGH1234")]     // upper + digit only
    [InlineData("!!!!!!!!####")]     // symbol only
    // Degenerate input.
    [InlineData("")]
    [InlineData(null)]
    public void IsComplexEnough_False_ForWeakPasswords(string? password) =>
        Assert.False(PasswordPolicy.IsComplexEnough(password));
}
