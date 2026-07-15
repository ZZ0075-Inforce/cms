using CMS.API.Controllers;
using CMS.API.Models.Lookups;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;

namespace CMS.API.Tests.Controllers;

/// <summary>
/// HTTP-contract tests for the lookup endpoints the FeaturedPromoItem board relies on: the centre-tab
/// list and the PromoCode → Promotion resolve (200 when found, 404 when the code is unknown).
/// </summary>
public class LookupsControllerTests
{
    private readonly ILookupRepository _repository = Substitute.For<ILookupRepository>();
    private readonly LookupsController _controller;

    public LookupsControllerTests() => _controller = new LookupsController(_repository);

    [Fact]
    public async Task GetTrainingCenters_ReturnsOk_WithTheTabList()
    {
        _repository.GetTrainingCentersAsync(Arg.Any<CancellationToken>()).Returns(new[]
        {
            new TrainingCenterLookup { Pkid = 1, Name = "台北" },
            new TrainingCenterLookup { Pkid = 2, Name = "新竹" }
        });

        var result = await _controller.GetTrainingCenters(default);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var centers = Assert.IsAssignableFrom<IReadOnlyList<TrainingCenterLookup>>(ok.Value);
        Assert.Equal(2, centers.Count);
        Assert.Equal("台北", centers[0].Name);
    }

    [Fact]
    public async Task GetPromotionByCode_ReturnsOk_WithTheResolvedPromotion()
    {
        _repository.GetPromotionByCodeAsync("20251215_n8n", Arg.Any<CancellationToken>()).Returns(
            new PromotionLookup
            {
                Pkid = 10,
                PromoCode = "20251215_n8n",
                Topic = "n8n自動化三部曲",
                Description = "從自動化新手到企業級AI架構師學習路徑"
            });

        var result = await _controller.GetPromotionByCode("20251215_n8n", default);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var promotion = Assert.IsType<PromotionLookup>(ok.Value);
        Assert.Equal(10, promotion.Pkid);   // this pkid becomes FeaturedPromoItem.Promotion_pkid
    }

    [Fact]
    public async Task GetPromotionByCode_Returns404_WhenTheCodeIsUnknown()
    {
        _repository.GetPromotionByCodeAsync("does-not-exist", Arg.Any<CancellationToken>())
            .Returns((PromotionLookup?)null);

        var result = await _controller.GetPromotionByCode("does-not-exist", default);

        var notFound = Assert.IsType<NotFoundObjectResult>(result.Result);
        Assert.Equal("找不到促銷代碼", Assert.IsType<ProblemDetails>(notFound.Value).Title);
    }
}
