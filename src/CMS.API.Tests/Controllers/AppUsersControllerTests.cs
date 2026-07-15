using CMS.API.Controllers;
using CMS.API.Models;
using CMS.API.Repositories;
using CMS.API.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace CMS.API.Tests.Controllers;

/// <summary>
/// HTTP-contract tests. The repository is mocked, so these never touch SQL and are always green —
/// they pin status codes, the UserId-as-identity decision, and the reset-password route.
/// </summary>
public class AppUsersControllerTests
{
    private readonly IAppUserRepository _repository = Substitute.For<IAppUserRepository>();
    private readonly AppUsersController _controller;

    public AppUsersControllerTests() => _controller = new AppUsersController(_repository);

    private static AppUser User(string userId = "admin", int pkid = 1) => new()
    {
        Pkid = pkid,
        UserId = userId,
        UserName = "Administrator",
        IsActive = true,
        RoleCount = 2
    };

    private static AppUserRequest Request(string userId = "editor@uuu.com.tw") => new()
    {
        UserId = userId,
        UserName = "Editor",
        IsActive = true,
        RoleIds = ["Admin"]
    };

    // ---------- GET /api/app-users ----------

    [Fact]
    public async Task GetAll_ReturnsOk_WithUsers()
    {
        _repository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns([User("admin"), User("helen", 2)]);

        var result = await _controller.GetAll(default);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var users = Assert.IsAssignableFrom<IReadOnlyList<AppUser>>(ok.Value);
        Assert.Equal(2, users.Count);
        Assert.Equal("admin", users[0].UserId);
        Assert.Equal(2, users[0].RoleCount);
    }

    // ---------- POST /api/app-users/query ----------

    [Fact]
    public async Task Query_ForwardsFilters_ToRepository()
    {
        var query = new AppUserQuery { Keyword = "adm", IsActive = true };
        _repository.QueryAsync(query, Arg.Any<CancellationToken>()).Returns([User()]);

        var result = await _controller.Query(query, default);

        Assert.IsType<OkObjectResult>(result.Result);
        await _repository.Received(1).QueryAsync(
            Arg.Is<AppUserQuery>(q => q.Keyword == "adm" && q.IsActive == true),
            Arg.Any<CancellationToken>());
    }

    // ---------- GET /api/app-users/{id} ----------

    [Fact]
    public async Task GetById_ReturnsOk_WithRoleIds_WhenFound()
    {
        var user = User();
        user.RoleIds = ["Admin", "Editor"];
        _repository.GetByIdAsync("admin", Arg.Any<CancellationToken>()).Returns(user);

        var result = await _controller.GetById("admin", default);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var returned = Assert.IsType<AppUser>(ok.Value);
        Assert.Equal(["Admin", "Editor"], returned.RoleIds);
    }

    [Fact]
    public async Task GetById_Returns404_WhenMissing()
    {
        _repository.GetByIdAsync("ghost", Arg.Any<CancellationToken>()).Returns((AppUser?)null);

        var result = await _controller.GetById("ghost", default);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    // ---------- POST /api/app-users ----------

    [Fact]
    public async Task Create_Returns201_WithLocationKeyedOnUserId_NotPkid()
    {
        var request = Request("editor@uuu.com.tw");
        _repository.ExistsAsync("editor@uuu.com.tw", Arg.Any<CancellationToken>()).Returns(false);
        _repository.InsertAsync(request, Arg.Any<CancellationToken>()).Returns(42);
        _repository.GetByIdAsync("editor@uuu.com.tw", Arg.Any<CancellationToken>())
            .Returns(new AppUser { Pkid = 42, UserId = "editor@uuu.com.tw", UserName = "Editor" });

        var result = await _controller.Create(request, default);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(nameof(AppUsersController.GetById), created.ActionName);
        // The route value must be the UserId, never the IDENTITY pkid.
        Assert.Equal("editor@uuu.com.tw", created.RouteValues!["id"]);
        Assert.NotEqual("42", created.RouteValues["id"]?.ToString());
    }

    [Fact]
    public async Task Create_Returns409_WhenUserIdAlreadyExists()
    {
        _repository.ExistsAsync("admin", Arg.Any<CancellationToken>()).Returns(true);

        var result = await _controller.Create(Request("admin"), default);

        var conflict = Assert.IsType<ConflictObjectResult>(result.Result);
        var problem = Assert.IsType<ProblemDetails>(conflict.Value);
        Assert.Equal("使用者代碼重複", problem.Title);
        await _repository.DidNotReceive().InsertAsync(Arg.Any<AppUserRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_Returns409_WhenInsertRacesAndThrowsDuplicateKey()
    {
        // ExistsAsync says "free", but another writer wins the race before our INSERT lands.
        var request = Request("editor@uuu.com.tw");
        _repository.ExistsAsync("editor@uuu.com.tw", Arg.Any<CancellationToken>()).Returns(false);
        _repository.InsertAsync(request, Arg.Any<CancellationToken>())
            .Throws(SqlExceptionFactory.Create(2627));

        var result = await _controller.Create(request, default);

        Assert.IsType<ConflictObjectResult>(result.Result);
    }

    [Fact]
    public async Task Create_Returns400_WhenRoleIdViolatesForeignKey()
    {
        var request = Request("editor@uuu.com.tw");
        _repository.ExistsAsync("editor@uuu.com.tw", Arg.Any<CancellationToken>()).Returns(false);
        _repository.InsertAsync(request, Arg.Any<CancellationToken>())
            .Throws(SqlExceptionFactory.Create(547));

        var result = await _controller.Create(request, default);

        var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("角色不存在", Assert.IsType<ProblemDetails>(bad.Value).Title);
    }

    // ---------- PUT /api/app-users ----------

    [Fact]
    public async Task Update_ReadsUserIdFromBody_AndReturns204()
    {
        var request = Request("editor@uuu.com.tw");
        _repository.UpdateAsync(request, Arg.Any<CancellationToken>()).Returns(true);

        var result = await _controller.Update(request, default);

        Assert.IsType<NoContentResult>(result);
        await _repository.Received(1).UpdateAsync(
            Arg.Is<AppUserRequest>(r => r.UserId == "editor@uuu.com.tw"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_Returns404_WhenNoRowsAffected()
    {
        var request = Request("ghost");
        _repository.UpdateAsync(request, Arg.Any<CancellationToken>()).Returns(false);

        var result = await _controller.Update(request, default);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task Update_Returns400_WhenRoleIdViolatesForeignKey()
    {
        var request = Request("editor@uuu.com.tw");
        _repository.UpdateAsync(request, Arg.Any<CancellationToken>())
            .Throws(SqlExceptionFactory.Create(547));

        var result = await _controller.Update(request, default);

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("角色不存在", Assert.IsType<ProblemDetails>(bad.Value).Title);
    }

    // ---------- DELETE /api/app-users/{id} ----------

    [Fact]
    public async Task Delete_Returns204_WhenDeleted()
    {
        _repository.DeleteAsync("editor@uuu.com.tw", Arg.Any<CancellationToken>()).Returns(true);

        Assert.IsType<NoContentResult>(await _controller.Delete("editor@uuu.com.tw", default));
    }

    [Fact]
    public async Task Delete_Returns404_WhenMissing()
    {
        _repository.DeleteAsync("ghost", Arg.Any<CancellationToken>()).Returns(false);

        Assert.IsType<NotFoundObjectResult>(await _controller.Delete("ghost", default));
    }

    // ---------- POST /api/app-users/{id}/reset-password ----------

    [Fact]
    public async Task ResetPassword_Returns204_WhenUserExists()
    {
        _repository.ResetPasswordAsync("admin", Arg.Any<CancellationToken>()).Returns(true);

        Assert.IsType<NoContentResult>(await _controller.ResetPassword("admin", default));
        await _repository.Received(1).ResetPasswordAsync("admin", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResetPassword_Returns404_WhenMissing()
    {
        _repository.ResetPasswordAsync("ghost", Arg.Any<CancellationToken>()).Returns(false);

        Assert.IsType<NotFoundObjectResult>(await _controller.ResetPassword("ghost", default));
    }
}
