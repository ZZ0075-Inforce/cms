using System.Reflection;
using Microsoft.Data.SqlClient;

namespace CMS.API.Tests.Infrastructure;

/// <summary>
/// Fabricates a <see cref="SqlException"/> carrying a chosen error number.
/// The driver exposes no public constructor, so we go through its internal factory. This is the
/// only way to unit-test the controller's `catch (SqlException) when (ex.Number is …)` filters
/// without a live database.
/// </summary>
public static class SqlExceptionFactory
{
    public static SqlException Create(int number, string message = "test-sql-error")
    {
        var error = NewSqlError(number, message);

        var collection = (SqlErrorCollection)Activator.CreateInstance(
            typeof(SqlErrorCollection), nonPublic: true)!;

        // SqlErrorCollection.Add(SqlError) is internal.
        typeof(SqlErrorCollection)
            .GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(collection, [error]);

        // internal static SqlException CreateException(SqlErrorCollection, string serverVersion)
        var createException = typeof(SqlException)
            .GetMethod(
                "CreateException",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: [typeof(SqlErrorCollection), typeof(string)],
                modifiers: null)
            ?? throw new InvalidOperationException(
                "SqlException.CreateException(SqlErrorCollection, string) not found — " +
                "the Microsoft.Data.SqlClient internals changed.");

        return (SqlException)createException.Invoke(null, [collection, "16.0.0"])!;
    }

    /// <summary>
    /// SqlError's constructors are internal and their signatures have drifted between driver
    /// versions, so bind by shape rather than an exact parameter list. Crucially, each argument is
    /// built from the parameter's declared TYPE — binding purely by name breaks the moment a
    /// version declares, say, win32ErrorCode as uint rather than int.
    /// </summary>
    private static SqlError NewSqlError(int number, string message)
    {
        var ctor = typeof(SqlError)
            .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .OrderByDescending(c => c.GetParameters().Length)
            .First();

        var args = ctor.GetParameters().Select(object? (p) =>
        {
            var name = p.Name ?? string.Empty;
            var type = p.ParameterType;

            // The error number is the only value under test; coerce it to whatever width the
            // parameter actually declares.
            if (name.Contains("number", StringComparison.OrdinalIgnoreCase) &&
                !name.Contains("line", StringComparison.OrdinalIgnoreCase) &&
                type != typeof(string))
            {
                return Convert.ChangeType(number, Nullable.GetUnderlyingType(type) ?? type);
            }

            if (type == typeof(string))
                return name.Contains("message", StringComparison.OrdinalIgnoreCase)
                    ? message
                    : string.Empty;

            if (p.HasDefaultValue) return p.DefaultValue;

            return type.IsValueType ? Activator.CreateInstance(type) : null;
        }).ToArray();

        return (SqlError)ctor.Invoke(args);
    }
}
