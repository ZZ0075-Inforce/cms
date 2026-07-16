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
/// HTTP-contract tests. The repository is mocked, so these never touch SQL — they pin status codes and
/// the client-supplied-pkid decision: unlike Partner (IDENTITY, body pkid ignored), PublishStatus's
/// pkid comes from the body and a duplicate is a real 409.
/// </summary>
public class PublishStatusesControllerTests
{
    private readonly IPublishStatusRepository _repository = Substitute.For<IPublishStatusRepository>();
    private readonly PublishStatusesController _controller;

    public PublishStatusesControllerTests() => _controller = new PublishStatusesController(_repository);

    private static PublishStatus Status(byte pkid = 1, string description = "草稿") => new()
    {
        Pkid = pkid,
        Description = description,
        IsDraft = true,
        IsPublished = false,
        IsDiscontinued = false,
        CourseCount = 3
    };

    private static PublishStatusRequest Request(byte pkid = 9, string description = "已上架") => new()
    {
        Pkid = pkid,
        Description = description,
        IsDraft = false,
        IsPublished = true,
        IsDiscontinued = false
    };

    // ---------- GET /api/publish-statuses ----------

    [Fact]
    public async Task GetAll_ReturnsOk_WithStatuses()
    {
        _repository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns([Status(), Status(2, "已上架")]);

        var result = await _controller.GetAll(default);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var statuses = Assert.IsAssignableFrom<IReadOnlyList<PublishStatus>>(ok.Value);
        Assert.Equal(2, statuses.Count);
        Assert.Equal("草稿", statuses[0].Description);
        Assert.Equal(3, statuses[0].CourseCount);
    }

    // ---------- POST /api/publish-statuses/query ----------

    [Fact]
    public async Task Query_ForwardsFilter_ToRepository()
    {
        var query = new PublishStatusQuery { Keyword = "上架", IsPublished = true };
        _repository.QueryAsync(query, Arg.Any<CancellationToken>()).Returns([Status(2, "已上架")]);

        var result = await _controller.Query(query, default);

        Assert.IsType<OkObjectResult>(result.Result);
        await _repository.Received(1).QueryAsync(
            Arg.Is<PublishStatusQuery>(q => q.Keyword == "上架" && q.IsPublished == true),
            Arg.Any<CancellationToken>());
    }

    // ---------- GET /api/publish-statuses/{id} ----------

    [Fact]
    public async Task GetById_ReturnsOk_WhenFound()
    {
        _repository.GetByIdAsync((byte)1, Arg.Any<CancellationToken>()).Returns(Status());

        var result = await _controller.GetById(1, default);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal("草稿", Assert.IsType<PublishStatus>(ok.Value).Description);
    }

    [Fact]
    public async Task GetById_Returns404_WhenMissing()
    {
        _repository.GetByIdAsync((byte)99, Arg.Any<CancellationToken>()).Returns((PublishStatus?)null);

        var result = await _controller.GetById(99, default);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    // ---------- POST /api/publish-statuses ----------

    [Fact]
    public async Task Create_Returns201_WithLocationKeyedOnTheClientSuppliedPkid()
    {
        var request = Request(pkid: 9);
        _repository.ExistsAsync((byte)9, Arg.Any<CancellationToken>()).Returns(false);
        _repository.InsertAsync(request, Arg.Any<CancellationToken>()).Returns((byte)9);
        _repository.GetByIdAsync((byte)9, Arg.Any<CancellationToken>()).Returns(Status(9, "已上架"));

        var result = await _controller.Create(request, default);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(nameof(PublishStatusesController.GetById), created.ActionName);
        // Unlike Partner, the identity IS the client-supplied pkid — not a DB-generated one.
        Assert.Equal((byte)9, created.RouteValues!["id"]);
        Assert.Equal("已上架", Assert.IsType<PublishStatus>(created.Value).Description);
    }

    [Fact]
    public async Task Create_Returns409_WhenPkidAlreadyExists()
    {
        var request = Request(pkid: 1);
        _repository.ExistsAsync((byte)1, Arg.Any<CancellationToken>()).Returns(true);

        var result = await _controller.Create(request, default);

        var conflict = Assert.IsType<ConflictObjectResult>(result.Result);
        Assert.Equal("主代碼重複", Assert.IsType<ProblemDetails>(conflict.Value).Title);
        await _repository.DidNotReceive().InsertAsync(Arg.Any<PublishStatusRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_Returns409_WhenInsertRacesToADuplicateKey()
    {
        // Backstop for the TOCTOU race between ExistsAsync and the INSERT: a 2627 must still be a 409.
        var request = Request(pkid: 1);
        _repository.ExistsAsync((byte)1, Arg.Any<CancellationToken>()).Returns(false);
        _repository.InsertAsync(request, Arg.Any<CancellationToken>())
            .Throws(SqlExceptionFactory.Create(2627));

        var result = await _controller.Create(request, default);

        var conflict = Assert.IsType<ConflictObjectResult>(result.Result);
        Assert.Equal("主代碼重複", Assert.IsType<ProblemDetails>(conflict.Value).Title);
    }

    // ---------- PUT /api/publish-statuses ----------

    [Fact]
    public async Task Update_ReadsPkidFromBody_AndReturns204()
    {
        var request = Request(pkid: 3);
        _repository.UpdateAsync(request, Arg.Any<CancellationToken>()).Returns(true);

        var result = await _controller.Update(request, default);

        Assert.IsType<NoContentResult>(result);
        await _repository.Received(1).UpdateAsync(
            Arg.Is<PublishStatusRequest>(r => r.Pkid == 3), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_Returns404_WhenNoRowsAffected()
    {
        var request = Request(pkid: 99);
        _repository.UpdateAsync(request, Arg.Any<CancellationToken>()).Returns(false);

        Assert.IsType<NotFoundObjectResult>(await _controller.Update(request, default));
    }

    // ---------- DELETE /api/publish-statuses/{id} ----------

    [Fact]
    public async Task Delete_Returns204_WhenDeleted()
    {
        _repository.DeleteAsync((byte)1, Arg.Any<CancellationToken>()).Returns(true);

        Assert.IsType<NoContentResult>(await _controller.Delete(1, default));
    }

    [Fact]
    public async Task Delete_Returns404_WhenMissing()
    {
        _repository.DeleteAsync((byte)99, Arg.Any<CancellationToken>()).Returns(false);

        Assert.IsType<NotFoundObjectResult>(await _controller.Delete(99, default));
    }

    [Fact]
    public async Task Delete_Returns409_WhenCoursesStillReferenceTheStatus()
    {
        // FK_Course_PublishStatus does not cascade, so SQL throws 547. That must surface as a conflict
        // the user can act on, not a 500.
        _repository.DeleteAsync((byte)1, Arg.Any<CancellationToken>())
            .Throws(SqlExceptionFactory.Create(547));

        var result = await _controller.Delete(1, default);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal("上架狀態使用中", Assert.IsType<ProblemDetails>(conflict.Value).Title);
    }
}
