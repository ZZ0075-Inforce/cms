using CMS.API.Controllers;
using CMS.API.Models;
using CMS.API.Repositories;
using CMS.API.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace CMS.API.Tests.Controllers;

/// <summary>
/// HTTP-contract tests. The repository is mocked, so these never touch SQL — they pin status codes
/// and the pkid-as-identity decision (the opposite of AppRole, where pkid is NOT the key).
/// </summary>
public class PartnersControllerTests
{
    private readonly IPartnerRepository _repository = Substitute.For<IPartnerRepository>();
    private readonly PartnersController _controller;

    public PartnersControllerTests() => _controller = new PartnersController(_repository);

    private static Partner Partner(short pkid = 1, string name = "Microsoft") => new()
    {
        Pkid = pkid,
        Name = name,
        AppKey = "MS",
        NameOnPartnerMenu = "Microsoft 微軟",
        NameOnCourseDetailPage = "微軟",
        DisplayOrder = 10,
        ImageFilename = "ms.png",
        CourseCount = 7
    };

    private static PartnerRequest Request(short pkid = 0, string name = "Cisco") => new()
    {
        Pkid = pkid,
        Name = name,
        AppKey = "CSCO",
        NameOnPartnerMenu = "Cisco 思科",
        NameOnCourseDetailPage = "思科",
        DisplayOrder = 20,
        ImageFilename = null
    };

    // ---------- GET /api/partners ----------

    [Fact]
    public async Task GetAll_ReturnsOk_WithPartners()
    {
        _repository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns([Partner(), Partner(2, "Cisco")]);

        var result = await _controller.GetAll(default);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var partners = Assert.IsAssignableFrom<IReadOnlyList<Partner>>(ok.Value);
        Assert.Equal(2, partners.Count);
        Assert.Equal("Microsoft", partners[0].Name);
        Assert.Equal(7, partners[0].CourseCount);
    }

    // ---------- POST /api/partners/query ----------

    [Fact]
    public async Task Query_ForwardsKeyword_ToRepository()
    {
        var query = new PartnerQuery { Keyword = "micro" };
        _repository.QueryAsync(query, Arg.Any<CancellationToken>()).Returns([Partner()]);

        var result = await _controller.Query(query, default);

        Assert.IsType<OkObjectResult>(result.Result);
        await _repository.Received(1).QueryAsync(
            Arg.Is<PartnerQuery>(q => q.Keyword == "micro"), Arg.Any<CancellationToken>());
    }

    // ---------- GET /api/partners/{id} ----------

    [Fact]
    public async Task GetById_ReturnsOk_WhenFound()
    {
        _repository.GetByIdAsync((short)1, Arg.Any<CancellationToken>()).Returns(Partner());

        var result = await _controller.GetById(1, default);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal("Microsoft", Assert.IsType<Partner>(ok.Value).Name);
    }

    [Fact]
    public async Task GetById_Returns404_WhenMissing()
    {
        _repository.GetByIdAsync((short)99, Arg.Any<CancellationToken>()).Returns((Partner?)null);

        var result = await _controller.GetById(99, default);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    // ---------- POST /api/partners ----------

    [Fact]
    public async Task Create_Returns201_WithLocationKeyedOnTheGeneratedPkid()
    {
        var request = Request();
        _repository.InsertAsync(request, Arg.Any<CancellationToken>()).Returns((short)42);
        _repository.GetByIdAsync((short)42, Arg.Any<CancellationToken>()).Returns(Partner(42, "Cisco"));

        var result = await _controller.Create(request, default);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(nameof(PartnersController.GetById), created.ActionName);
        // Unlike AppRole, the identity IS the pkid — the DB-generated one, not anything from the body.
        Assert.Equal((short)42, created.RouteValues!["id"]);
        Assert.Equal("Cisco", Assert.IsType<Partner>(created.Value).Name);
    }

    [Fact]
    public async Task Create_IgnoresAnyPkidSuppliedInTheBody()
    {
        // pkid is IDENTITY. A client-supplied value must not decide the new row's key.
        var request = Request(pkid: 999);
        _repository.InsertAsync(request, Arg.Any<CancellationToken>()).Returns((short)7);
        _repository.GetByIdAsync((short)7, Arg.Any<CancellationToken>()).Returns(Partner(7, "Cisco"));

        var result = await _controller.Create(request, default);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal((short)7, created.RouteValues!["id"]);
    }

    // ---------- PUT /api/partners ----------

    [Fact]
    public async Task Update_ReadsPkidFromBody_AndReturns204()
    {
        var request = Request(pkid: 3);
        _repository.UpdateAsync(request, Arg.Any<CancellationToken>()).Returns(true);

        var result = await _controller.Update(request, default);

        Assert.IsType<NoContentResult>(result);
        await _repository.Received(1).UpdateAsync(
            Arg.Is<PartnerRequest>(r => r.Pkid == 3), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_Returns404_WhenNoRowsAffected()
    {
        var request = Request(pkid: 99);
        _repository.UpdateAsync(request, Arg.Any<CancellationToken>()).Returns(false);

        Assert.IsType<NotFoundObjectResult>(await _controller.Update(request, default));
    }

    // ---------- DELETE /api/partners/{id} ----------

    [Fact]
    public async Task Delete_Returns204_WhenDeleted()
    {
        _repository.DeleteAsync((short)1, Arg.Any<CancellationToken>()).Returns(true);

        Assert.IsType<NoContentResult>(await _controller.Delete(1, default));
    }

    [Fact]
    public async Task Delete_Returns404_WhenMissing()
    {
        _repository.DeleteAsync((short)99, Arg.Any<CancellationToken>()).Returns(false);

        Assert.IsType<NotFoundObjectResult>(await _controller.Delete(99, default));
    }

    [Fact]
    public async Task Delete_Returns409_WhenChildRowsStillReferenceThePartner()
    {
        // No FK into Partner cascades, so SQL throws 547. That must surface as a conflict the user
        // can act on, not a 500.
        _repository.DeleteAsync((short)1, Arg.Any<CancellationToken>())
            .Throws(SqlExceptionFactory.Create(547));

        var result = await _controller.Delete(1, default);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal("廠商使用中", Assert.IsType<ProblemDetails>(conflict.Value).Title);
    }
}
