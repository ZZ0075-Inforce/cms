namespace CMS.API.Infrastructure;

/// <summary>
/// The new-password complexity rule shared by the change-password flow: at least 8 characters AND at
/// least three of the four character classes (uppercase / lowercase / digit / symbol). The Angular form
/// mirrors this rule client-side (core/utils/password.validator.ts); keep the two in step.
///
/// "Symbol" is any character that is neither a letter nor a digit, so both sides agree on ASCII input.
/// </summary>
public static class PasswordPolicy
{
    public const int MinLength = 8;

    /// <summary>The bilingual message surfaced (in the UI) whenever a new password fails this rule.</summary>
    public const string ViolationMessage =
        "密碼長度至少需 8 碼，且內容須至少包含四種字元的其中三種：大寫英文／小寫英文／數字／符號\n" +
        "(Password must be at least 8 characters and contain at least 3 of the 4 classes: " +
        "uppercase / lowercase / digit / symbol.)";

    public static bool IsComplexEnough(string? password)
    {
        if (password is null || password.Length < MinLength)
            return false;

        var classes = 0;
        if (password.Any(char.IsUpper)) classes++;
        if (password.Any(char.IsLower)) classes++;
        if (password.Any(char.IsDigit)) classes++;
        if (password.Any(c => !char.IsLetterOrDigit(c))) classes++;

        return classes >= 3;
    }
}
