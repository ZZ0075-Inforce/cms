using CMS.API.Models;
using CMS.API.Repositories;
using CMS.API.Tests.Infrastructure;
using Dapper;

namespace CMS.API.Tests.Repositories;

/// <summary>
/// Runs real SQL against the real CMS database. The parts a mock cannot catch: the Promotion2 /
/// TrainingCenter JOIN labels, the DateOnly round-trip, the one-week ScheduleOn range filter, the
/// TrainingCenter tab filter, the PromoCode → Promotion resolve, and the transaction-wrapped slot swap
/// against the live UNIQUE (ScheduleOn, TrainingCenter, Slot) index.
///
/// It seeds its own TEST_ Promotion2 as a valid FK target and borrows existing TrainingCenter pkids
/// read-only (that table is fixed reference data — 台北 / 新竹 / … — never TEST-owned or mutated).
///
/// Run without a DB:  dotnet test --filter "Category!=Integration"
/// </summary>
[Trait("Category", "Integration")]
[Collection(DatabaseCollection.Name)]
public class FeaturedPromoItemRepositoryIntegrationTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private readonly FeaturedPromoItemRepository _repository = new(fixture.ConnectionFactory);
    private readonly LookupRepository _lookups = new(fixture.ConnectionFactory);
    private readonly List<int> _created = [];

    private int _promotionPkid;
    private string _promoCode = string.Empty;
    private short _centerPkid;
    private short _otherCenterPkid;

    private const string PromoTopic = "TEST_n8n自動化三部曲";
    private const string PromoDescription = "從自動化新手到企業級AI架構師學習路徑";

    public async Task InitializeAsync()
    {
        if (!DatabaseProbe.IsAvailable) return;

        _promoCode = DatabaseFixture.NewPromoCode();
        var publishStatusPkid = await fixture.AnyPublishStatusPkidAsync();

        await using var conn = await fixture.OpenAsync();

        // A real Promotion2 for the required FK — nullable image/flash/partner columns stay NULL.
        _promotionPkid = await conn.ExecuteScalarAsync<int>("""
            INSERT INTO dbo.Promotion2
                (PublishStatus_pkid, ObjectId, PromoCode, Topic, Description, DisplayOrder, ScheduleOn, ScheduleOff)
            VALUES
                (@Ps, NEWID(), @Code, @Topic, @Desc, 0, @On, @Off);
            SELECT CAST(SCOPE_IDENTITY() AS int);
            """, new
        {
            Ps = publishStatusPkid,
            Code = _promoCode,
            Topic = PromoTopic,
            Desc = PromoDescription,
            On = new DateOnly(2099, 1, 1),
            Off = new DateOnly(2099, 12, 31)
        });

        // Borrow two distinct centres (read-only) so the tab filter can prove cross-centre exclusion.
        var centers = (await conn.QueryAsync<short>(
            "SELECT TOP 2 pkid FROM dbo.TrainingCenter ORDER BY pkid;")).AsList();
        _centerPkid = centers[0];
        _otherCenterPkid = centers.Count > 1 ? centers[1] : centers[0];
    }

    /// <summary>Removes only what this test created, in FK order (items before their Promotion2).</summary>
    public async Task DisposeAsync()
    {
        if (!DatabaseProbe.IsAvailable) return;

        foreach (var pkid in _created)
            await _repository.DeleteAsync(pkid);

        await using var conn = await fixture.OpenAsync();
        await conn.ExecuteAsync("DELETE FROM dbo.Promotion2 WHERE pkid = @Pkid;", new { Pkid = _promotionPkid });
    }

    private FeaturedPromoItemRequest NewRequest(DateOnly scheduleOn, byte slot, short? center = null) => new()
    {
        ScheduleOn = scheduleOn,
        TrainingCenterPkid = center ?? _centerPkid,
        Slot = slot,
        PromotionPkid = _promotionPkid,
        Topic = PromoTopic,             // TEST_-prefixed so the fixture sweep reclaims stragglers
        Description = PromoDescription
    };

    private async Task<int> InsertAsync(FeaturedPromoItemRequest request)
    {
        var pkid = await _repository.InsertAsync(request);
        _created.Add(pkid);
        request.Pkid = pkid;
        return pkid;
    }

    // ---------- Insert + GetById (JOIN labels, DateOnly round-trip) ----------

    [IntegrationFact]
    public async Task InsertAsync_PersistsRow_AndGetByIdResolvesTheJoinLabels()
    {
        var pkid = await InsertAsync(NewRequest(new DateOnly(2099, 3, 16), slot: 1));

        var item = await _repository.GetByIdAsync(pkid);

        Assert.NotNull(item);
        Assert.Equal(new DateOnly(2099, 3, 16), item.ScheduleOn);   // DateOnly round-trip
        Assert.Equal((byte)1, item.Slot);
        Assert.Equal(_promoCode, item.PromoCode);                    // Promotion2 JOIN label
        Assert.False(string.IsNullOrEmpty(item.TrainingCenterName)); // TrainingCenter JOIN label
    }

    // ---------- One-week ScheduleOn filter ----------

    [IntegrationFact]
    public async Task QueryAsync_OneWeekScheduleOnRange_KeepsInWeek_ExcludesOutOfWeek()
    {
        var monday = await InsertAsync(NewRequest(new DateOnly(2099, 3, 16), slot: 1));   // week start
        var midWeek = await InsertAsync(NewRequest(new DateOnly(2099, 3, 18), slot: 1));  // inside the week
        var nextWeek = await InsertAsync(NewRequest(new DateOnly(2099, 3, 25), slot: 1)); // the following week

        var results = await _repository.QueryAsync(new FeaturedPromoItemQuery
        {
            TrainingCenterPkid = _centerPkid,
            ScheduleOnFrom = new DateOnly(2099, 3, 16),   // Monday
            ScheduleOnTo = new DateOnly(2099, 3, 22)      // Sunday
        });

        Assert.Contains(results, i => i.Pkid == monday);
        Assert.Contains(results, i => i.Pkid == midWeek);
        Assert.DoesNotContain(results, i => i.Pkid == nextWeek);
    }

    // ---------- TrainingCenter tab filter ----------

    [IntegrationFact]
    public async Task QueryAsync_FiltersByTrainingCenter_AndExcludesOtherCentres()
    {
        var date = new DateOnly(2099, 4, 6);
        var mine = await InsertAsync(NewRequest(date, slot: 1, center: _centerPkid));

        var results = await _repository.QueryAsync(new FeaturedPromoItemQuery { TrainingCenterPkid = _centerPkid });

        Assert.Contains(results, i => i.Pkid == mine);
        Assert.All(results, i => Assert.Equal(_centerPkid, i.TrainingCenterPkid));

        if (_otherCenterPkid != _centerPkid)
        {
            var other = await InsertAsync(NewRequest(date, slot: 1, center: _otherCenterPkid));
            var filtered = await _repository.QueryAsync(new FeaturedPromoItemQuery { TrainingCenterPkid = _centerPkid });
            Assert.DoesNotContain(filtered, i => i.Pkid == other);
        }
    }

    // ---------- PromoCode lookup ----------

    [IntegrationFact]
    public async Task GetPromotionByCodeAsync_ResolvesTheSeededPromotion()
    {
        var promotion = await _lookups.GetPromotionByCodeAsync(_promoCode);

        Assert.NotNull(promotion);
        Assert.Equal(_promotionPkid, promotion.Pkid);   // this pkid becomes FeaturedPromoItem.Promotion_pkid
        Assert.Equal(PromoTopic, promotion.Topic);
        Assert.Equal(PromoDescription, promotion.Description);
    }

    [IntegrationFact]
    public async Task GetPromotionByCodeAsync_ReturnsNull_WhenTheCodeIsUnknown()
    {
        Assert.Null(await _lookups.GetPromotionByCodeAsync($"{DatabaseFixture.Prefix}no_such_code_x"));
    }

    // ---------- Update / Delete ----------

    [IntegrationFact]
    public async Task UpdateAsync_ChangesTopicAndDescription()
    {
        var request = NewRequest(new DateOnly(2099, 5, 4), slot: 1);
        var pkid = await InsertAsync(request);

        request.Topic = "TEST_更新後標題";
        request.Description = "更新後說明";
        var updated = await _repository.UpdateAsync(request);

        Assert.True(updated);
        var item = await _repository.GetByIdAsync(pkid);
        Assert.NotNull(item);
        Assert.Equal("TEST_更新後標題", item.Topic);
        Assert.Equal("更新後說明", item.Description);
    }

    [IntegrationFact]
    public async Task UpdateAsync_ReturnsFalse_WhenPkidMissing()
    {
        var request = NewRequest(new DateOnly(2099, 5, 5), slot: 1);
        request.Pkid = -1;

        Assert.False(await _repository.UpdateAsync(request));
    }

    [IntegrationFact]
    public async Task DeleteAsync_RemovesTheRow()
    {
        var pkid = await InsertAsync(NewRequest(new DateOnly(2099, 6, 1), slot: 1));

        Assert.True(await _repository.DeleteAsync(pkid));
        Assert.Null(await _repository.GetByIdAsync(pkid));
    }

    // ---------- Slot move (transaction-wrapped swap) ----------

    [IntegrationFact]
    public async Task MoveSlotAsync_SwapsWithTheOccupantOfTheTargetSlot()
    {
        var date = new DateOnly(2099, 7, 6);
        var slot1 = await InsertAsync(NewRequest(date, slot: 1));
        var slot2 = await InsertAsync(NewRequest(date, slot: 2));

        // Move slot-1 row down (+1): it swaps with the slot-2 occupant, no UNIQUE-index clash.
        var result = await _repository.MoveSlotAsync(slot1, +1);

        Assert.Equal(SlotMoveResult.Moved, result);
        Assert.Equal((byte)2, (await _repository.GetByIdAsync(slot1))!.Slot);
        Assert.Equal((byte)1, (await _repository.GetByIdAsync(slot2))!.Slot);
    }

    [IntegrationFact]
    public async Task MoveSlotAsync_MovesIntoAnEmptySlot_WhenTargetIsFree()
    {
        var pkid = await InsertAsync(NewRequest(new DateOnly(2099, 7, 13), slot: 1));

        var result = await _repository.MoveSlotAsync(pkid, +1);   // slot 2 is free

        Assert.Equal(SlotMoveResult.Moved, result);
        Assert.Equal((byte)2, (await _repository.GetByIdAsync(pkid))!.Slot);
    }

    [IntegrationFact]
    public async Task MoveSlotAsync_ReturnsOutOfRange_AtTheBoundary()
    {
        var pkid = await InsertAsync(NewRequest(new DateOnly(2099, 7, 20), slot: 1));

        Assert.Equal(SlotMoveResult.OutOfRange, await _repository.MoveSlotAsync(pkid, -1));   // slot 1 → 0
    }

    [IntegrationFact]
    public async Task MoveSlotAsync_ReturnsNotFound_WhenMissing()
    {
        Assert.Equal(SlotMoveResult.NotFound, await _repository.MoveSlotAsync(-1, +1));
    }
}
