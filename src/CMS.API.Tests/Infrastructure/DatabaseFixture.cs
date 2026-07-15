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

    /// <summary>Removes every TEST_-prefixed row — and only those, in FK-dependency order.</summary>
    private async Task CleanupAsync()
    {
        // Order matters — a child must go before the parent it FKs (none of these cascade the way we
        // need). Course goes before Partner/CourseGroup/Certification/JobCategory because it FKs the
        // first two and its cascade clears the CourseInCertification / CourseJobCategories rows that
        // would otherwise pin the last two. Certification FKs Partner, so it precedes Partner too.
        //
        // Keyed on whichever column has room for the TEST_ prefix: Partner.Name (AppKey is varchar(10),
        // too short), Course.CourseId (varchar(50)), Certification.Title (nchar(100)), the rest Description.
        //
        // FeaturedPromoItem is a leaf keyed on Topic (nvarchar, room for the prefix); it FKs
        // Promotion2, so it is swept first. Promotion2's own PromoCode carries the prefix.
        const string sql = """
            DELETE FROM dbo.FeaturedPromoItem WHERE Topic     LIKE 'TEST\_%' ESCAPE '\';
            DELETE FROM dbo.Promotion2        WHERE PromoCode LIKE 'TEST\_%' ESCAPE '\';
            DELETE FROM dbo.AppUserRole   WHERE RoleId LIKE 'TEST\_%' ESCAPE '\'
                                             OR UserId LIKE 'TEST\_%' ESCAPE '\';
            DELETE FROM dbo.AppRole       WHERE RoleId      LIKE 'TEST\_%' ESCAPE '\';
            DELETE FROM dbo.AppUser       WHERE UserId      LIKE 'TEST\_%' ESCAPE '\';
            DELETE FROM dbo.Course        WHERE CourseId    LIKE 'TEST\_%' ESCAPE '\';
            DELETE FROM dbo.Certification WHERE Title       LIKE 'TEST\_%' ESCAPE '\';
            DELETE FROM dbo.JobCategory   WHERE Description  LIKE 'TEST\_%' ESCAPE '\';
            DELETE FROM dbo.Partner       WHERE Name         LIKE 'TEST\_%' ESCAPE '\';
            DELETE FROM dbo.CourseGroup   WHERE Description  LIKE 'TEST\_%' ESCAPE '\';
            """;

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        await conn.ExecuteAsync(sql);
    }

    /// <summary>
    /// One existing PublishStatus pkid, for Course FK seeding. PublishStatus is a fixed enum table
    /// (rows are not TEST-owned and are never mutated); the real CMS DB always has rows.
    /// </summary>
    public async Task<byte> AnyPublishStatusPkidAsync()
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        return await conn.ExecuteScalarAsync<byte>(
            "SELECT TOP 1 pkid FROM dbo.PublishStatus ORDER BY pkid;");
    }

    /// <summary>
    /// One existing TrainingCenter pkid, for FeaturedPromoItem FK seeding. TrainingCenter is a fixed
    /// reference table (台北 / 新竹 / …) — rows are not TEST-owned and are never mutated.
    /// </summary>
    public async Task<short> AnyTrainingCenterPkidAsync()
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        return await conn.ExecuteScalarAsync<short>(
            "SELECT TOP 1 pkid FROM dbo.TrainingCenter ORDER BY pkid;");
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

    /// <summary>A collision-proof Partner.Name — the column cleanup keys on. nvarchar(50).</summary>
    public static string NewPartnerName() => $"{Prefix}{Guid.NewGuid():N}"[..24];

    /// <summary>Partner.AppKey is varchar(10) and has no UNIQUE constraint — length is all that matters.</summary>
    public static string NewAppKey() => Guid.NewGuid().ToString("N")[..10];

    /// <summary>A collision-proof CourseGroup.Description — the column cleanup keys on. nvarchar(100).</summary>
    public static string NewCourseGroupName() => $"{Prefix}{Guid.NewGuid():N}"[..24];

    /// <summary>A collision-proof Course.CourseId — the column cleanup keys on. varchar(50).</summary>
    public static string NewCourseId() => $"{Prefix}{Guid.NewGuid():N}"[..24];

    /// <summary>A collision-proof, unique Promotion2.PromoCode — the column cleanup keys on. nvarchar(30).</summary>
    public static string NewPromoCode() => $"{Prefix}{Guid.NewGuid():N}"[..24];
}

[CollectionDefinition(DatabaseCollection.Name)]
public class DatabaseCollection : ICollectionFixture<DatabaseFixture>
{
    public const string Name = "Database";
}
