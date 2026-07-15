using CMS.API.Controllers;
using CMS.API.Models;
using CMS.API.Repositories;
using CMS.API.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace CMS.API.Tests.Controllers;

/// <summary>
/// HTTP-contract tests. The repository is mocked, so these never touch SQL — they pin status codes
/// and, above all, the 409-on-547 behaviour that keeps a still-referenced group from 500ing.
/// </summary>
public class CourseGroupsControllerTests
{
    private readonly ICourseGroupRepository _repository = Substitute.For<ICourseGroupRepository>();
    private readonly CourseGroupsController _controller;

    public CourseGroupsControllerTests() => _controller = new CourseGroupsController(_repository);

    private static CourseGroup Group(short pkid = 1, string description = "雲端") => new()
    {
        Pkid = pkid,
        Description = description,
        CourseCount = 7
    };

    private static CourseGroupRequest Request(short pkid = 0, string description = "資安") => new()
    {
        Pkid = pkid,
        Description = description
    };

    // ---------- GET /api/course-groups ----------

    [Fact]
    public async Task GetAll_ReturnsOk_WithGroups()
    {
        _repository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns([Group(), Group(2, "資安")]);

        var result = await _controller.GetAll(default);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var groups = Assert.IsAssignableFrom<IReadOnlyList<CourseGroup>>(ok.Value);
        Assert.Equal(2, groups.Count);
        Assert.Equal("雲端", groups[0].Description);
        Assert.Equal(7, groups[0].CourseCount);
    }

    // ---------- POST /api/course-groups/query ----------

    [Fact]
    public async Task Query_ForwardsKeyword_ToRepository()
    {
        var query = new CourseGroupQuery { Keyword = "雲" };
        _repository.QueryAsync(query, Arg.Any<CancellationToken>()).Returns([Group()]);

        var result = await _controller.Query(query, default);

        Assert.IsType<OkObjectResult>(result.Result);
        await _repository.Received(1).QueryAsync(
            Arg.Is<CourseGroupQuery>(q => q.Keyword == "雲"), Arg.Any<CancellationToken>());
    }

    // ---------- GET /api/course-groups/{id} ----------

    [Fact]
    public async Task GetById_ReturnsOk_WhenFound()
    {
        _repository.GetByIdAsync((short)1, Arg.Any<CancellationToken>()).Returns(Group());

        var result = await _controller.GetById(1, default);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal("雲端", Assert.IsType<CourseGroup>(ok.Value).Description);
    }

    [Fact]
    public async Task GetById_Returns404_WhenMissing()
    {
        _repository.GetByIdAsync((short)99, Arg.Any<CancellationToken>()).Returns((CourseGroup?)null);

        var result = await _controller.GetById(99, default);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    // ---------- POST /api/course-groups ----------

    [Fact]
    public async Task Create_Returns201_WithLocationKeyedOnTheGeneratedPkid()
    {
        var request = Request();
        _repository.InsertAsync(request, Arg.Any<CancellationToken>()).Returns((short)42);
        _repository.GetByIdAsync((short)42, Arg.Any<CancellationToken>()).Returns(Group(42, "資安"));

        var result = await _controller.Create(request, default);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(nameof(CourseGroupsController.GetById), created.ActionName);
        Assert.Equal((short)42, created.RouteValues!["id"]);
        Assert.Equal("資安", Assert.IsType<CourseGroup>(created.Value).Description);
    }

    [Fact]
    public async Task Create_IgnoresAnyPkidSuppliedInTheBody()
    {
        // pkid is IDENTITY. A client-supplied value must not decide the new row's key.
        var request = Request(pkid: 999);
        _repository.InsertAsync(request, Arg.Any<CancellationToken>()).Returns((short)7);
        _repository.GetByIdAsync((short)7, Arg.Any<CancellationToken>()).Returns(Group(7, "資安"));

        var result = await _controller.Create(request, default);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal((short)7, created.RouteValues!["id"]);
    }

    // ---------- PUT /api/course-groups ----------

    [Fact]
    public async Task Update_ReadsPkidFromBody_AndReturns204()
    {
        var request = Request(pkid: 3);
        _repository.UpdateAsync(request, Arg.Any<CancellationToken>()).Returns(true);

        var result = await _controller.Update(request, default);

        Assert.IsType<NoContentResult>(result);
        await _repository.Received(1).UpdateAsync(
            Arg.Is<CourseGroupRequest>(r => r.Pkid == 3), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_Returns404_WhenNoRowsAffected()
    {
        var request = Request(pkid: 99);
        _repository.UpdateAsync(request, Arg.Any<CancellationToken>()).Returns(false);

        Assert.IsType<NotFoundObjectResult>(await _controller.Update(request, default));
    }

    // ---------- DELETE /api/course-groups/{id} ----------

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
    public async Task Delete_Returns409_WhenAPartnerCourseGroupStillReferencesTheGroup()
    {
        // FK_PartnerCourseGroup_CourseGroup does not cascade, so SQL throws 547. That must surface as
        // a conflict, not a 500. (Course rows would cascade and never reach here.)
        _repository.DeleteAsync((short)1, Arg.Any<CancellationToken>())
            .Throws(SqlExceptionFactory.Create(547));

        var result = await _controller.Delete(1, default);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal("群組使用中", Assert.IsType<ProblemDetails>(conflict.Value).Title);
    }
}
