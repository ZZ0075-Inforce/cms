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
/// the FK-violation → 400 on write, and the 547 → 409 on delete.
/// </summary>
public class CoursesControllerTests
{
    private readonly ICourseRepository _repository = Substitute.For<ICourseRepository>();
    private readonly CoursesController _controller;

    public CoursesControllerTests() => _controller = new CoursesController(_repository);

    private static Course Course(int pkid = 1, string title = "Azure 基礎") => new()
    {
        Pkid = pkid,
        Title = title,
        CourseId = "AZ-900",
        ProdCourseId = "P-AZ-900",
        FriendlyUrl = "azure-900",
        DisplayOrder = 10,
        PartnerPkid = 1,
        CourseGroupPkid = 2,
        PublishStatusPkid = 1,
        ScheduleOn = new DateOnly(2026, 1, 1),
        ScheduleOff = new DateOnly(2036, 1, 1),
        Hour = 21,
        ListPrice = 15000m,
        LearningCredit = 3.5m,
        CanRepeat = true,
        PartnerName = "Microsoft",
        CourseGroupName = "雲端",
        PublishStatusName = "上架",
        CertificationPkids = [5, 6],
        JobCategoryPkids = [7]
    };

    private static CourseRequest Request(int pkid = 0) => new()
    {
        Pkid = pkid,
        Title = "Cisco CCNA",
        CourseId = "CCNA",
        ProdCourseId = "P-CCNA",
        FriendlyUrl = "ccna",
        DisplayOrder = 20,
        PartnerPkid = 2,
        CourseGroupPkid = null,
        PublishStatusPkid = 1,
        ScheduleOn = new DateOnly(2026, 2, 1),
        ScheduleOff = new DateOnly(2036, 2, 1),
        Hour = 35,
        ListPrice = 30000m,
        LearningCredit = 5m,
        CanRepeat = false,
        CertificationPkids = [1],
        JobCategoryPkids = [2, 3]
    };

    // ---------- GET /api/courses ----------

    [Fact]
    public async Task GetAll_ReturnsOk_WithCourses()
    {
        _repository.GetAllAsync(Arg.Any<CancellationToken>()).Returns([Course(), Course(2, "CCNA")]);

        var result = await _controller.GetAll(default);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var courses = Assert.IsAssignableFrom<IReadOnlyList<Course>>(ok.Value);
        Assert.Equal(2, courses.Count);
        Assert.Equal("Microsoft", courses[0].PartnerName);   // JOIN-resolved label survives
    }

    // ---------- POST /api/courses/query ----------

    [Fact]
    public async Task Query_ForwardsFilter_ToRepository()
    {
        var query = new CourseQuery { Keyword = "azure", PartnerPkid = 1, CanRepeat = true };
        _repository.QueryAsync(query, Arg.Any<CancellationToken>()).Returns([Course()]);

        var result = await _controller.Query(query, default);

        Assert.IsType<OkObjectResult>(result.Result);
        await _repository.Received(1).QueryAsync(
            Arg.Is<CourseQuery>(q => q.Keyword == "azure" && q.PartnerPkid == 1 && q.CanRepeat == true),
            Arg.Any<CancellationToken>());
    }

    // ---------- GET /api/courses/{id} ----------

    [Fact]
    public async Task GetById_ReturnsOk_WithNnLists_WhenFound()
    {
        _repository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(Course());

        var result = await _controller.GetById(1, default);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var course = Assert.IsType<Course>(ok.Value);
        Assert.Equal(new[] { 5, 6 }, course.CertificationPkids);
        Assert.Equal(new short[] { 7 }, course.JobCategoryPkids);
    }

    [Fact]
    public async Task GetById_Returns404_WhenMissing()
    {
        _repository.GetByIdAsync(99, Arg.Any<CancellationToken>()).Returns((Course?)null);

        var result = await _controller.GetById(99, default);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    // ---------- POST /api/courses ----------

    [Fact]
    public async Task Create_Returns201_WithLocationKeyedOnTheGeneratedPkid()
    {
        var request = Request();
        _repository.InsertAsync(request, Arg.Any<CancellationToken>()).Returns(42);
        _repository.GetByIdAsync(42, Arg.Any<CancellationToken>()).Returns(Course(42, "CCNA"));

        var result = await _controller.Create(request, default);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(nameof(CoursesController.GetById), created.ActionName);
        Assert.Equal(42, created.RouteValues!["id"]);
    }

    [Fact]
    public async Task Create_Returns400_WhenAReferencedPkidDoesNotExist()
    {
        // A bad Partner/CourseGroup/PublishStatus/Certification/JobCategory pkid trips FK 547.
        _repository.InsertAsync(Arg.Any<CourseRequest>(), Arg.Any<CancellationToken>())
            .Throws(SqlExceptionFactory.Create(547));

        var result = await _controller.Create(Request(), default);

        var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("關聯資料不存在", Assert.IsType<ProblemDetails>(bad.Value).Title);
    }

    // ---------- PUT /api/courses ----------

    [Fact]
    public async Task Update_ReadsPkidFromBody_AndReturns204()
    {
        var request = Request(pkid: 3);
        _repository.UpdateAsync(request, Arg.Any<CancellationToken>()).Returns(true);

        var result = await _controller.Update(request, default);

        Assert.IsType<NoContentResult>(result);
        await _repository.Received(1).UpdateAsync(
            Arg.Is<CourseRequest>(r => r.Pkid == 3), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_Returns404_WhenNoRowsAffected()
    {
        var request = Request(pkid: 99);
        _repository.UpdateAsync(request, Arg.Any<CancellationToken>()).Returns(false);

        Assert.IsType<NotFoundObjectResult>(await _controller.Update(request, default));
    }

    [Fact]
    public async Task Update_Returns400_WhenAReferencedPkidDoesNotExist()
    {
        _repository.UpdateAsync(Arg.Any<CourseRequest>(), Arg.Any<CancellationToken>())
            .Throws(SqlExceptionFactory.Create(547));

        var result = await _controller.Update(Request(pkid: 3), default);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    // ---------- DELETE /api/courses/{id} ----------

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

    [Fact]
    public async Task Delete_Returns409_WhenChildRowsStillReferenceTheCourse()
    {
        // CourseFAQ / CourseRelatedLink / HotCourse do not cascade, so SQL throws 547. That must
        // surface as a conflict, not a 500. (The N-N junctions cascade and never reach here.)
        _repository.DeleteAsync(1, Arg.Any<CancellationToken>())
            .Throws(SqlExceptionFactory.Create(547));

        var result = await _controller.Delete(1, default);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal("課程使用中", Assert.IsType<ProblemDetails>(conflict.Value).Title);
    }
}
