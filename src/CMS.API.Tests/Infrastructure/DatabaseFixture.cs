using System.Data.Common;
using CMS.API.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace CMS.API.Tests.Infrastructure;

/// <summary>
/// Shared connection factory for integration tests, plus seeding and cleanup of test-owned rows.
///
/// CMS is the REAL database with real seeded rows (Admin, User, and a live AppUser). Tests must
/// never mutate or delete those, and must never assert an exact row count — only containment.
/// Everything a test owns is prefixed TEST_ and is the only thing cleanup touches.
///
/// Isolation is by unique key + guaranteed cleanup rather than an ambient TransactionScope: the
/// repository opens its own SqlTransaction (it must, for N-N atomicity), and wrapping that in an
/// ambient transaction either conflicts or escalates to MSDTC.
/// </summary>
public sealed class DatabaseFixture : IAsyncLifetime
{
    public const string Prefix = "TEST_";

    /// <summary>AppUsers this fixture owns. Seeded so N-N tests don't depend on production data.</summary>
    public static readonly string[] TestUserIds = [$"{Prefix}user_a", $"{Prefix}user_b"];

    public IDbConnectionFactory ConnectionFactory { get; }

    private readonly string _connectionString = TestConnectionString.Value;

    public DatabaseFixture()
    {
        DapperConfig.Register();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:CMS"] = _connectionString
            })
            .Build();

        ConnectionFactory = new SqlConnectionFactory(config);
    }

    public async Task InitializeAsync()
    {
        if (!DatabaseProbe.IsAvailable) return;

        // Sweep leftovers from a run that was killed mid-test, then seed fresh.
        await CleanupAsync();
        await SeedTestUsersAsync();
    }

    public async Task DisposeAsync()
    {
        if (DatabaseProbe.IsAvailable) await CleanupAsync();
    }

    public async Task<DbConnection> OpenAsync() => await ConnectionFactory.CreateOpenConnectionAsync();

    /// <summary>Removes every TEST_-prefixed row — and only those. Junction first (no cascade).</summary>
    private async Task CleanupAsync()
    {
        const string sql = """
            DELETE FROM dbo.AppUserRole WHERE RoleId LIKE 'TEST\_%' ESCAPE '\'
                                           OR UserId LIKE 'TEST\_%' ESCAPE '\';
            DELETE FROM dbo.AppRole     WHERE RoleId LIKE 'TEST\_%' ESCAPE '\';
            DELETE FROM dbo.AppUser     WHERE UserId LIKE 'TEST\_%' ESCAPE '\';
            """;

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        await conn.ExecuteAsync(sql);
    }

    private async Task SeedTestUsersAsync()
    {
        const string sql = """
            INSERT INTO dbo.AppUser (UserId, UserName, IsActive, PasswordHash)
            VALUES (@UserId, @UserName, 1, 'test-not-a-real-hash');
            """;

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        await conn.ExecuteAsync(sql, TestUserIds
            .Select(id => new { UserId = id, UserName = $"Test User {id}" }));
    }

    /// <summary>A collision-proof RoleId that also satisfies the ^[A-Za-z0-9_\-]+$ rule.</summary>
    public static string NewRoleId() => $"{Prefix}{Guid.NewGuid():N}"[..24];
}

[CollectionDefinition(DatabaseCollection.Name)]
public class DatabaseCollection : ICollectionFixture<DatabaseFixture>
{
    public const string Name = "Database";
}
