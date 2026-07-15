using CMS.API.Controllers;
using CMS.API.Models;
using CMS.API.Repositories;
using CMS.API.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace CMS.API.Tests.Controllers;

/// <summary>
/// HTTP-contract tests. The repository is mocked, so these never touch SQL — they pin status codes,
/// that the one-week ScheduleOn range and the TrainingCenter tab reach the repository unchanged, the
/// FK-violation → 400 and duplicate-slot → 409 on write, and the slot-move status mapping.
/// </summary>
public class FeaturedPromoItemsControllerTests
{
    private readonly IFeaturedPromoItemRepository _repository =
        Substitute.For<IFeaturedPromoItemRepository>();
    private readonly FeaturedPromoItemsController _controller;

    public FeaturedPromoItemsControllerTests() => _controller = new FeaturedPromoItemsController(_repository);

    private static FeaturedPromoItem Item(int pkid = 1, byte slot = 1) => new()
    {
        Pkid = pkid,
        ScheduleOn = new DateOnly(2026, 3, 16),
        TrainingCenterPkid = 1,
        Slot = slot,
        PromotionPkid = 10,
        Topic = "n8n自動化三部曲",
        Description = "從自動化新手到企業級AI架構師學習路徑",
        PromoCode = "20251215_n8n",
        TrainingCenterName = "台北"
    };

    private static FeaturedPromoItemRequest Request(int pkid = 0, byte slot = 1) => new()
    {
        Pkid = pkid,
        ScheduleOn = new DateOnly(2026, 3, 16),
        TrainingCenterPkid = 1,
        Slot = slot,
        PromotionPkid = 10,
        Topic = "n8n自動化三部曲",
        Description = "從自動化新手到企業級AI架構師學習路徑"
    };

    // ---------- POST /api/featured-promo-items/query ----------

    [Fact]
    public async Task Query_ForwardsTheWeekAndCentreFilter_ToRepository()
    {
        var query = new FeaturedPromoItemQuery
        {
            TrainingCenterPkid = 1,
            ScheduleOnFrom = new DateOnly(2026, 3, 16),   // Monday
            ScheduleOnTo = new DateOnly(2026, 3, 22)      // Sunday
        };
        _repository.QueryAsync(query, Arg.Any<CancellationToken>()).Returns([Item()]);

        var result = await _controller.Query(query, default);

        Assert.IsType<OkObjectResult>(result.Result);
        await _repository.Received(1).QueryAsync(
            Arg.Is<FeaturedPromoItemQuery>(q =>
                q.TrainingCenterPkid == 1
                && q.ScheduleOnFrom == new DateOnly(2026, 3, 16)
                && q.ScheduleOnTo == new DateOnly(2026, 3, 22)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAll_ReturnsOk_WithJoinLabels()
    {
        _repository.GetAllAsync(Arg.Any<CancellationToken>()).Returns([Item()]);

        var result = await _controller.GetAll(default);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var items = Assert.IsAssignableFrom<IReadOnlyList<FeaturedPromoItem>>(ok.Value);
        Assert.Equal("20251215_n8n", items[0].PromoCode);   // JOIN-resolved label survives
    }

    // ---------- GET /api/featured-promo-items/{id} ----------

    [Fact]
    public async Task GetById_ReturnsOk_WhenFound()
    {
        _repository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(Item());

        var result = await _controller.GetById(1, default);

        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetById_Returns404_WhenMissing()
    {
        _repository.GetByIdAsync(99, Arg.Any<CancellationToken>()).Returns((FeaturedPromoItem?)null);

        Assert.IsType<NotFoundObjectResult>((await _controller.GetById(99, default)).Result);
    }

    // ---------- POST /api/featured-promo-items ----------

    [Fact]
    public async Task Create_Returns201_WithLocationKeyedOnTheGeneratedPkid()
    {
        var request = Request();
        _repository.InsertAsync(request, Arg.Any<CancellationToken>()).Returns(42);
        _repository.GetByIdAsync(42, Arg.Any<CancellationToken>()).Returns(Item(42));

        var result = await _controller.Create(request, default);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(nameof(FeaturedPromoItemsController.GetById), created.ActionName);
        Assert.Equal(42, created.RouteValues!["id"]);
    }

    [Fact]
    public async Task Create_Returns400_WhenAReferencedPkidDoesNotExist()
    {
        // A bad Promotion / TrainingCenter pkid trips FK 547.
        _repository.InsertAsync(Arg.Any<FeaturedPromoItemRequest>(), Arg.Any<CancellationToken>())
            .Throws(SqlExceptionFactory.Create(547));

        var result = await _controller.Create(Request(), default);

        var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("關聯資料不存在", Assert.IsType<ProblemDetails>(bad.Value).Title);
    }

    [Fact]
    public async Task Create_Returns409_WhenTheSlotIsAlreadyTaken()
    {
        // The UNIQUE (ScheduleOn, TrainingCenter, Slot) index throws 2627 on a duplicate.
        _repository.InsertAsync(Arg.Any<FeaturedPromoItemRequest>(), Arg.Any<CancellationToken>())
            .Throws(SqlExceptionFactory.Create(2627));

        var result = await _controller.Create(Request(), default);

        var conflict = Assert.IsType<ConflictObjectResult>(result.Result);
        Assert.Equal("版位已被占用", Assert.IsType<ProblemDetails>(conflict.Value).Title);
    }

    // ---------- PUT /api/featured-promo-items ----------

    [Fact]
    public async Task Update_ReadsPkidFromBody_AndReturns204()
    {
        var request = Request(pkid: 3);
        _repository.UpdateAsync(request, Arg.Any<CancellationToken>()).Returns(true);

        var result = await _controller.Update(request, default);

        Assert.IsType<NoContentResult>(result);
        await _repository.Received(1).UpdateAsync(
            Arg.Is<FeaturedPromoItemRequest>(r => r.Pkid == 3), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_Returns404_WhenNoRowsAffected()
    {
        _repository.UpdateAsync(Arg.Any<FeaturedPromoItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(false);

        Assert.IsType<NotFoundObjectResult>(await _controller.Update(Request(pkid: 99), default));
    }

    [Fact]
    public async Task Update_Returns409_WhenMovedOntoAnOccupiedSlot()
    {
        _repository.UpdateAsync(Arg.Any<FeaturedPromoItemRequest>(), Arg.Any<CancellationToken>())
            .Throws(SqlExceptionFactory.Create(2601));

        Assert.IsType<ConflictObjectResult>(await _controller.Update(Request(pkid: 3), default));
    }

    // ---------- DELETE /api/featured-promo-items/{id} ----------

    [Fact]
    public async Task Delete_Returns204_WhenDeleted()
    {
        _repository.DeleteAsync(1, Arg.Any<CancellationToken>()).Returns(true);

        Assert.IsType<NoContentResult>(await _controller.Delete(1, default));
    }

    [Fact]
    public async Task Delete_Returns404_WhenMissing()
    {
        _repository.DeleteAsync(99, Arg.Any<CancellationToken>()).Returns(false);

        Assert.IsType<NotFoundObjectResult>(await _controller.Delete(99, default));
    }

    // ---------- POST /api/featured-promo-items/{id}/move ----------

    [Theory]
    [InlineData("down", 1)]
    [InlineData("up", -1)]
    public async Task Move_MapsDirectionToDelta_AndReturns204(string direction, int expectedDelta)
    {
        _repository.MoveSlotAsync(5, expectedDelta, Arg.Any<CancellationToken>())
            .Returns(SlotMoveResult.Moved);

        var result = await _controller.Move(5, new FeaturedPromoItemMoveRequest { Direction = direction }, default);

        Assert.IsType<NoContentResult>(result);
        await _repository.Received(1).MoveSlotAsync(5, expectedDelta, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Move_Returns400_WhenTheDirectionIsNeitherUpNorDown()
    {
        var result = await _controller.Move(5, new FeaturedPromoItemMoveRequest { Direction = "sideways" }, default);

        Assert.IsType<BadRequestObjectResult>(result);
        await _repository.DidNotReceive().MoveSlotAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Move_Returns404_WhenTheRowIsMissing()
    {
        _repository.MoveSlotAsync(5, 1, Arg.Any<CancellationToken>()).Returns(SlotMoveResult.NotFound);

        var result = await _controller.Move(5, new FeaturedPromoItemMoveRequest { Direction = "down" }, default);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task Move_Returns400_AtASlotBoundary()
    {
        _repository.MoveSlotAsync(5, 1, Arg.Any<CancellationToken>()).Returns(SlotMoveResult.OutOfRange);

        var result = await _controller.Move(5, new FeaturedPromoItemMoveRequest { Direction = "down" }, default);

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("無法移動版位", Assert.IsType<ProblemDetails>(bad.Value).Title);
    }
}
