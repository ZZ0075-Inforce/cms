using Microsoft.Data.SqlClient;

namespace CMS.API.Tests.Infrastructure;

/// <summary>
/// A <see cref="FactAttribute"/> that skips (rather than fails) when SQL Server is unreachable,
/// so the suite stays runnable on a machine without SQLEXPRESS.
///
/// xUnit 2.x has no runtime <c>Assert.Skip</c>, so the decision is made at discovery time via
/// the <see cref="FactAttribute.Skip"/> property. Connectivity is probed once per process.
/// </summary>
public sealed class IntegrationFactAttribute : FactAttribute
{
    public IntegrationFactAttribute()
    {
        if (!DatabaseProbe.IsAvailable)
            Skip = $"SQL Server (.\\SQLEXPRESS) unavailable: {DatabaseProbe.Reason}";
    }
}

/// <inheritdoc cref="IntegrationFactAttribute"/>
public sealed class IntegrationTheoryAttribute : TheoryAttribute
{
    public IntegrationTheoryAttribute()
    {
        if (!DatabaseProbe.IsAvailable)
            Skip = $"SQL Server (.\\SQLEXPRESS) unavailable: {DatabaseProbe.Reason}";
    }
}

internal static class DatabaseProbe
{
    private static readonly Lazy<(bool Available, string? Reason)> Probe = new(() =>
    {
        try
        {
            using var conn = new SqlConnection(TestConnectionString.Value);
            conn.Open();
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    });

    public static bool IsAvailable => Probe.Value.Available;
    public static string? Reason => Probe.Value.Reason;
}

internal static class TestConnectionString
{
    public static string Value =>
        Environment.GetEnvironmentVariable("ConnectionStrings__CMS")
        ?? "Server=.\\SQLEXPRESS;Database=CMS;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False";
}
