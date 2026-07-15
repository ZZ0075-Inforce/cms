namespace CMS.API.Infrastructure;

/// <summary>LIKE-pattern helpers shared by every repository's keyword filter.</summary>
public static class SqlLike
{
    /// <summary>
    /// Escapes LIKE metacharacters before wrapping in wildcards. Without this a keyword of
    /// "%" or "_" silently matches every row. Pairs with ESCAPE '\' in the SQL.
    /// </summary>
    public static string ToPattern(string keyword) =>
        "%" + keyword
            .Replace("\\", "\\\\")
            .Replace("%", "\\%")
            .Replace("_", "\\_")
            .Replace("[", "\\[")
        + "%";
}
