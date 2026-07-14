namespace CMS.API.Infrastructure;

/// <summary>SQL Server error numbers we translate into HTTP status codes.</summary>
public static class SqlErrorNumbers
{
    /// <summary>Violation of PRIMARY KEY / UNIQUE constraint.</summary>
    public const int UniqueConstraintViolation = 2627;

    /// <summary>Cannot insert duplicate key row in an object with a unique index.</summary>
    public const int DuplicateKeyRow = 2601;

    /// <summary>FOREIGN KEY constraint violation.</summary>
    public const int ForeignKeyViolation = 547;

    public static bool IsDuplicateKey(int number) =>
        number is UniqueConstraintViolation or DuplicateKeyRow;
}
