using System.Collections;
using System.Data.Common;
using System.Reflection;
using CMS.API.Models;
using Dapper;
using Microsoft.AspNetCore.Http;

namespace CMS.API.Infrastructure;

/// <summary>
/// Reflection-based, entity-agnostic implementation of <see cref="IRowAuditWriter"/>. It derives every
/// non-caller-supplied column of a <see cref="RowAudit"/> row from the entity itself:
///
/// <list type="bullet">
///   <item><c>UserName</c> — the <c>userName</c> claim of the current request's JWT, or "system" when
///     there is no authenticated user.</item>
///   <item><c>PrimaryKeyValues</c> — the entity's <c>pkid</c> property, as a string.</item>
///   <item><c>ActionDesc</c> — Insert/Delete: the first string property's value; Update: the
///     comma-separated names of the properties that changed. Truncated to 1000 chars.</item>
/// </list>
///
/// The <c>Build*Entry</c> methods are pure (no DB) so the reflection rules can be unit-tested in
/// isolation; the <c>Log*Async</c> methods build an entry and INSERT it on the caller's connection.
/// </summary>
public sealed class RowAuditWriter(IHttpContextAccessor httpContextAccessor) : IRowAuditWriter
{
    /// <summary>Written as the actor when no authenticated user is on the request.</summary>
    public const string SystemUser = "system";

    /// <summary>Matches the ActionDesc column width; longer descriptions are truncated to fit.</summary>
    public const int MaxActionDescLength = 1000;

    private const string InsertSql = """
        INSERT INTO dbo.RowAudit
            (TableName, UserName, PrimaryKeyValues, ActionType, ActionDesc, [DateTime])
        VALUES
            (@TableName, @UserName, @PrimaryKeyValues, @ActionType, @ActionDesc, @DateTime);
        """;

    // ---------- IRowAuditWriter ----------

    public Task LogInsertAsync(
        DbConnection conn, DbTransaction? tx, string tableName, object entity, CancellationToken ct = default)
        => InsertAsync(conn, tx, BuildInsertEntry(tableName, entity), ct);

    public Task LogUpdateAsync(
        DbConnection conn, DbTransaction? tx, string tableName, object before, object after,
        CancellationToken ct = default)
    {
        var entry = BuildUpdateEntry(tableName, before, after);

        // Nothing changed → no audit row (an empty change list carries no information).
        if (string.IsNullOrEmpty(entry.ActionDesc)) return Task.CompletedTask;

        return InsertAsync(conn, tx, entry, ct);
    }

    public Task LogDeleteAsync(
        DbConnection conn, DbTransaction? tx, string tableName, object entity, CancellationToken ct = default)
        => InsertAsync(conn, tx, BuildDeleteEntry(tableName, entity), ct);

    // ---------- Entry building (pure; reflection only, no DB) ----------

    public RowAudit BuildInsertEntry(string tableName, object entity)
        => Build(tableName, "Insert", entity, FirstStringValue(entity));

    public RowAudit BuildUpdateEntry(string tableName, object before, object after)
        => Build(tableName, "Update", after, ChangedProperties(before, after));

    public RowAudit BuildDeleteEntry(string tableName, object entity)
        => Build(tableName, "Delete", entity, FirstStringValue(entity));

    private RowAudit Build(string tableName, string actionType, object pkidSource, string actionDesc) => new()
    {
        TableName = tableName,
        UserName = CurrentUserName(),
        PrimaryKeyValues = PkidValue(pkidSource),
        ActionType = actionType,
        ActionDesc = Truncate(actionDesc),
        DateTime = DateTime.Now
    };

    // ---------- Reflection helpers (public so the rules can be unit-tested directly) ----------

    /// <summary>The current request's <c>userName</c> claim, or <see cref="SystemUser"/> when absent.</summary>
    public string CurrentUserName()
    {
        var name = httpContextAccessor.HttpContext?.User.FindFirst(JwtTokenGenerator.UserNameClaim)?.Value;
        return string.IsNullOrWhiteSpace(name) ? SystemUser : name;
    }

    /// <summary>The entity's <c>pkid</c> property as a string, or "" when it has none.</summary>
    public static string PkidValue(object entity)
    {
        var pkid = ReadableProperties(entity.GetType())
            .FirstOrDefault(p => string.Equals(p.Name, "pkid", StringComparison.OrdinalIgnoreCase));
        return pkid?.GetValue(entity)?.ToString() ?? string.Empty;
    }

    /// <summary>The value of the entity's first string-typed property in declaration order, or "".</summary>
    public static string FirstStringValue(object entity)
    {
        var first = ReadableProperties(entity.GetType())
            .FirstOrDefault(p => p.PropertyType == typeof(string));
        return first?.GetValue(entity) as string ?? string.Empty;
    }

    /// <summary>
    /// Comma-separated names of the properties whose value differs between <paramref name="before"/> and
    /// <paramref name="after"/>, in declaration order. Empty when nothing changed.
    /// </summary>
    public static string ChangedProperties(object before, object after)
    {
        var changed = ReadableProperties(before.GetType())
            .Where(p => !ValuesEqual(p.GetValue(before), p.GetValue(after)))
            .Select(p => p.Name);
        return string.Join(",", changed);
    }

    /// <summary>Caps <paramref name="value"/> at <see cref="MaxActionDescLength"/> characters.</summary>
    public static string Truncate(string value)
        => value.Length <= MaxActionDescLength ? value : value[..MaxActionDescLength];

    // ---------- internals ----------

    /// <summary>
    /// Public, readable instance properties in declaration order. Reflection does not guarantee order,
    /// so we sort by metadata token, which is the declaration order the compiler emitted.
    /// </summary>
    private static IEnumerable<PropertyInfo> ReadableProperties(Type type)
        => type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
            .OrderBy(p => p.MetadataToken);

    /// <summary>
    /// Value equality that also handles collection-valued properties (e.g. the N-N pkid lists on Course):
    /// two non-string enumerables compare element-by-element rather than by reference.
    /// </summary>
    private static bool ValuesEqual(object? a, object? b)
    {
        if (a is null || b is null) return a is null && b is null;
        if (a is string || b is string) return Equals(a, b);
        if (a is IEnumerable ea && b is IEnumerable eb)
            return ea.Cast<object>().SequenceEqual(eb.Cast<object>());
        return Equals(a, b);
    }

    private static Task InsertAsync(DbConnection conn, DbTransaction? tx, RowAudit entry, CancellationToken ct)
        => conn.ExecuteAsync(new CommandDefinition(
            InsertSql,
            new
            {
                entry.TableName,
                entry.UserName,
                entry.PrimaryKeyValues,
                entry.ActionType,
                entry.ActionDesc,
                entry.DateTime
            },
            tx, cancellationToken: ct));
}
