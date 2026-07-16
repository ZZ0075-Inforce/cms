using System.Data.Common;
using CMS.API.Data;
using CMS.API.Infrastructure;
using CMS.API.Models;
using Dapper;

namespace CMS.API.Repositories;

public sealed class FeaturedPromoItemRepository(
    IDbConnectionFactory connectionFactory, IRowAuditWriter auditWriter)
    : IFeaturedPromoItemRepository
{
    private const string AuditTable = "FeaturedPromoItem";
    /// <summary>The mockup lays out three slots per day; a move must stay inside this range.</summary>
    private const int MinSlot = 1;
    private const int MaxSlot = 3;

    /// <summary>A transient slot used only inside the swap transaction, outside the 1..3 range.</summary>
    private const int TempSlot = 0;

    // Two FKs resolved to flat display labels: INNER JOIN Promotion2 (PromoCode, the value the board
    // shows per row) and TrainingCenter (Name). Both FKs are NOT NULL, so both are INNER JOINs.
    private const string SelectColumns = """
        SELECT f.pkid               AS Pkid,
               f.ScheduleOn,
               f.TrainingCenter_pkid AS TrainingCenterPkid,
               f.Slot,
               f.Promotion_pkid      AS PromotionPkid,
               f.Topic,
               f.Description,
               p.PromoCode           AS PromoCode,
               t.Name                AS TrainingCenterName
        FROM        dbo.FeaturedPromoItem f
        INNER JOIN  dbo.Promotion2      p ON p.pkid = f.Promotion_pkid
        INNER JOIN  dbo.TrainingCenter  t ON t.pkid = f.TrainingCenter_pkid
        """;

    private const string OrderBy =
        "ORDER BY f.ScheduleOn ASC, f.TrainingCenter_pkid ASC, f.Slot ASC, f.pkid ASC";

    public async Task<IReadOnlyList<FeaturedPromoItem>> GetAllAsync(CancellationToken ct = default)
    {
        const string sql = $"{SelectColumns} {OrderBy};";

        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<FeaturedPromoItem>(
            new CommandDefinition(sql, cancellationToken: ct));
        return rows.AsList();
    }

    public async Task<IReadOnlyList<FeaturedPromoItem>> QueryAsync(
        FeaturedPromoItemQuery query, CancellationToken ct = default)
    {
        // One static statement; every predicate is null-guarded so an omitted filter drops out. The
        // board always sends TrainingCenterPkid (active tab) + a Monday..Sunday ScheduleOn range.
        const string sql = $"""
            {SelectColumns}
            WHERE (@TrainingCenterPkid IS NULL OR f.TrainingCenter_pkid = @TrainingCenterPkid)
              AND (@ScheduleOnFrom     IS NULL OR f.ScheduleOn >= @ScheduleOnFrom)
              AND (@ScheduleOnTo       IS NULL OR f.ScheduleOn <= @ScheduleOnTo)
            {OrderBy};
            """;

        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<FeaturedPromoItem>(new CommandDefinition(
            sql,
            new
            {
                query.TrainingCenterPkid,
                query.ScheduleOnFrom,
                query.ScheduleOnTo
            },
            cancellationToken: ct));
        return rows.AsList();
    }

    public async Task<FeaturedPromoItem?> GetByIdAsync(int pkid, CancellationToken ct = default)
    {
        const string sql = $"{SelectColumns} WHERE f.pkid = @Pkid;";

        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<FeaturedPromoItem>(
            new CommandDefinition(sql, new { Pkid = pkid }, cancellationToken: ct));
    }

    public async Task<int> InsertAsync(FeaturedPromoItemRequest request, CancellationToken ct = default)
    {
        // A bad Promotion/TrainingCenter pkid trips FK 547; a duplicate (ScheduleOn, TrainingCenter,
        // Slot) trips the UNIQUE index (2627/2601). Both are left to propagate for the controller to
        // translate (400 and 409 respectively) — the transaction then rolls back, so a rejected insert
        // leaves no audit row.
        const string sql = """
            INSERT INTO dbo.FeaturedPromoItem
                (ScheduleOn, TrainingCenter_pkid, Slot, Promotion_pkid, Topic, Description)
            VALUES
                (@ScheduleOn, @TrainingCenterPkid, @Slot, @PromotionPkid, @Topic, @Description);
            SELECT CAST(SCOPE_IDENTITY() AS int);
            """;

        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        var pkid = await conn.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, Parameters(request), tx, cancellationToken: ct));

        var inserted = await LoadForAuditAsync(conn, tx, pkid, ct);
        await auditWriter.LogInsertAsync(conn, tx, AuditTable, inserted!, ct);

        await tx.CommitAsync(ct);
        return pkid;
    }

    public async Task<bool> UpdateAsync(FeaturedPromoItemRequest request, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE dbo.FeaturedPromoItem
            SET    ScheduleOn = @ScheduleOn, TrainingCenter_pkid = @TrainingCenterPkid, Slot = @Slot,
                   Promotion_pkid = @PromotionPkid, Topic = @Topic, Description = @Description
            WHERE  pkid = @Pkid;
            """;

        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        // Load the "before" first so the audit's changed-column list is accurate; a missing row is 404.
        var before = await LoadForAuditAsync(conn, tx, request.Pkid, ct);
        if (before is null)
        {
            await tx.RollbackAsync(ct);
            return false;
        }

        await conn.ExecuteAsync(
            new CommandDefinition(sql, Parameters(request, request.Pkid), tx, cancellationToken: ct));

        var after = await LoadForAuditAsync(conn, tx, request.Pkid, ct);
        await auditWriter.LogUpdateAsync(conn, tx, AuditTable, before, after!, ct);

        await tx.CommitAsync(ct);
        return true;
    }

    public async Task<bool> DeleteAsync(int pkid, CancellationToken ct = default)
    {
        const string sql = "DELETE FROM dbo.FeaturedPromoItem WHERE pkid = @Pkid;";

        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        // Load before deleting so the audit can record the row's first string column (Topic).
        var row = await LoadForAuditAsync(conn, tx, pkid, ct);
        if (row is null)
        {
            await tx.RollbackAsync(ct);
            return false;
        }

        await conn.ExecuteAsync(new CommandDefinition(sql, new { Pkid = pkid }, tx, cancellationToken: ct));
        await auditWriter.LogDeleteAsync(conn, tx, AuditTable, row, ct);

        await tx.CommitAsync(ct);
        return true;
    }

    /// <summary>
    /// Loads the row's own columns (no derived JOIN labels) as the audit before/after snapshot, on the
    /// caller's transaction so it sees the in-flight change.
    /// </summary>
    private static async Task<FeaturedPromoItem?> LoadForAuditAsync(
        DbConnection conn, DbTransaction tx, int pkid, CancellationToken ct)
        => await conn.QuerySingleOrDefaultAsync<FeaturedPromoItem>(new CommandDefinition("""
            SELECT pkid AS Pkid, ScheduleOn, TrainingCenter_pkid AS TrainingCenterPkid, Slot,
                   Promotion_pkid AS PromotionPkid, Topic, Description
            FROM   dbo.FeaturedPromoItem
            WHERE  pkid = @Pkid;
            """, new { Pkid = pkid }, tx, cancellationToken: ct));

    public async Task<SlotMoveResult> MoveSlotAsync(int pkid, int delta, CancellationToken ct = default)
    {
        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        var current = await conn.QuerySingleOrDefaultAsync<SlotRow>(new CommandDefinition("""
            SELECT pkid AS Pkid, ScheduleOn, TrainingCenter_pkid AS TrainingCenterPkid, Slot
            FROM   dbo.FeaturedPromoItem
            WHERE  pkid = @Pkid;
            """, new { Pkid = pkid }, tx, cancellationToken: ct));

        if (current is null)
        {
            await tx.RollbackAsync(ct);
            return SlotMoveResult.NotFound;
        }

        var targetSlot = current.Slot + delta;
        if (targetSlot < MinSlot || targetSlot > MaxSlot)
        {
            await tx.RollbackAsync(ct);
            return SlotMoveResult.OutOfRange;
        }

        // The row (if any) currently sitting in the slot we want to move into.
        var occupantPkid = await conn.ExecuteScalarAsync<int?>(new CommandDefinition("""
            SELECT pkid
            FROM   dbo.FeaturedPromoItem
            WHERE  ScheduleOn = @ScheduleOn AND TrainingCenter_pkid = @TrainingCenterPkid AND Slot = @Slot;
            """,
            new { current.ScheduleOn, current.TrainingCenterPkid, Slot = (byte)targetSlot },
            tx, cancellationToken: ct));

        if (occupantPkid is null)
        {
            await SetSlotAsync(conn, tx, pkid, targetSlot, ct);
        }
        else
        {
            // Park the moving row on a temporary slot first, so neither UPDATE collides with the
            // live UNIQUE (ScheduleOn, TrainingCenter, Slot) index mid-swap.
            await SetSlotAsync(conn, tx, pkid, TempSlot, ct);
            await SetSlotAsync(conn, tx, occupantPkid.Value, current.Slot, ct);
            await SetSlotAsync(conn, tx, pkid, targetSlot, ct);
        }

        await tx.CommitAsync(ct);
        return SlotMoveResult.Moved;
    }

    private static Task SetSlotAsync(
        System.Data.Common.DbConnection conn, System.Data.Common.DbTransaction tx,
        int pkid, int slot, CancellationToken ct)
        => conn.ExecuteAsync(new CommandDefinition(
            "UPDATE dbo.FeaturedPromoItem SET Slot = @Slot WHERE pkid = @Pkid;",
            new { Pkid = pkid, Slot = (byte)slot }, tx, cancellationToken: ct));

    private static object Parameters(FeaturedPromoItemRequest r, int? pkid = null) => new
    {
        Pkid = pkid,
        r.ScheduleOn,
        r.TrainingCenterPkid,
        r.Slot,
        r.PromotionPkid,
        Topic = r.Topic.Trim(),
        Description = r.Description.Trim()
    };

    private sealed class SlotRow
    {
        public int Pkid { get; set; }
        public DateOnly ScheduleOn { get; set; }
        public short TrainingCenterPkid { get; set; }
        public byte Slot { get; set; }
    }
}
