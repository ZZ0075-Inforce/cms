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
/// HTTP-contract tests. The repository is mocked, so these never touch SQL and are always green —
/// they pin status codes and the RoleId-as-identity decision.
/// </summary>
public class AppRolesControllerTests
{
    private readonly IAppRoleRepository _repository = Substitute.For<IAppRoleRepository>();
    private readonly AppRolesController _controller;

    public AppRolesControllerTests() => _controller = new AppRolesController(_repository);

    private static AppRole Role(string roleId = "Admin", int pkid = 1) => new()
    {
        Pkid = pkid,
        RoleId = roleId,
        RoleName = "Administrator",
        PermissionLevel = 1,
        Description = "系統管理員",
        UserCount = 3
    };

    private static AppRoleRequest Request(string roleId = "Editor") => new()
    {
        RoleId = roleId,
        RoleName = "Editor",
        PermissionLevel = 50,
        Description = "編輯",
        UserIds = ["alice"]
    };

    private static SqlException MakeSqlException(int number)
        => SqlExceptionFactory.Create(number);

    // ---------- GET /api/app-roles ----------

    [Fact]
    public async Task GetAll_ReturnsOk_WithRoles()
    {
        _repository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns([Role("Admin"), Role("User", 2)]);

        var result = await _controller.GetAll(default);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var roles = Assert.IsAssignableFrom<IReadOnlyList<AppRole>>(ok.Value);
        Assert.Equal(2, roles.Count);
        Assert.Equal("Admin", roles[0].RoleId);
        Assert.Equal(3, roles[0].UserCount);
    }

    // ---------- POST /api/app-roles/query ----------

    [Fact]
    public async Task Query_ForwardsFilters_ToRepository()
    {
        var query = new AppRoleQuery { Keyword = "adm", PermissionLevel = 1 };
        _repository.QueryAsync(query, Arg.Any<CancellationToken>()).Returns([Role()]);

        var result = await _controller.Query(query, default);

        Assert.IsType<OkObjectResult>(result.Result);
        await _repository.Received(1).QueryAsync(
            Arg.Is<AppRoleQuery>(q => q.Keyword == "adm" && q.PermissionLevel == 1),
            Arg.Any<CancellationToken>());
    }

    // ---------- GET /api/app-roles/{id} ----------

    [Fact]
    public async Task GetById_ReturnsOk_WithUserIds_WhenFound()
    {
        var role = Role();
        role.UserIds = ["alice", "bob"];
        _repository.GetByIdAsync("Admin", Arg.Any<CancellationToken>()).Returns(role);

        var result = await _controller.GetById("Admin", default);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var returned = Assert.IsType<AppRole>(ok.Value);
        Assert.Equal(["alice", "bob"], returned.UserIds);
    }

    [Fact]
    public async Task GetById_Returns404_WhenMissing()
    {
        _repository.GetByIdAsync("Ghost", Arg.Any<CancellationToken>()).Returns((AppRole?)null);

        var result = await _controller.GetById("Ghost", default);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    // ---------- POST /api/app-roles ----------

    [Fact]
    public async Task Create_Returns201_WithLocationKeyedOnRoleId_NotPkid()
    {
        var request = Request("Editor");
        _repository.ExistsAsync("Editor", Arg.Any<CancellationToken>()).Returns(false);
        _repository.InsertAsync(request, Arg.Any<CancellationToken>()).Returns(42);
        _repository.GetByIdAsync("Editor", Arg.Any<CancellationToken>())
            .Returns(new AppRole { Pkid = 42, RoleId = "Editor", RoleName = "Editor" });

        var result = await _controller.Create(request, default);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(nameof(AppRolesController.GetById), created.ActionName);
        // The route value must be the RoleId, never the IDENTITY pkid.
        Assert.Equal("Editor", created.RouteValues!["id"]);
        Assert.NotEqual("42", created.RouteValues["id"]?.ToString());
    }

    [Fact]
    public async Task Create_Returns409_WhenRoleIdAlreadyExists()
    {
        _repository.ExistsAsync("Admin", Arg.Any<CancellationToken>()).Returns(true);

        var result = await _controller.Create(Request("Admin"), default);

        var conflict = Assert.IsType<ConflictObjectResult>(result.Result);
        var problem = Assert.IsType<ProblemDetails>(conflict.Value);
        Assert.Equal("角色代碼重複", problem.Title);
        await _repository.DidNotReceive().InsertAsync(Arg.Any<AppRoleRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_Returns409_WhenInsertRacesAndThrowsDuplicateKey()
    {
        // ExistsAsync says "free", but another writer wins the race before our INSERT lands.
        var request = Request("Editor");
        _repository.ExistsAsync("Editor", Arg.Any<CancellationToken>()).Returns(false);
        _repository.InsertAsync(request, Arg.Any<CancellationToken>())
            .Throws(MakeSqlException(2627));

        var result = await _controller.Create(request, default);

        Assert.IsType<ConflictObjectResult>(result.Result);
    }

    [Fact]
    public async Task Create_Returns400_WhenUserIdViolatesForeignKey()
    {
        var request = Request("Editor");
        _repository.ExistsAsync("Editor", Arg.Any<CancellationToken>()).Returns(false);
        _repository.InsertAsync(request, Arg.Any<CancellationToken>())
            .Throws(MakeSqlException(547));

        var result = await _controller.Create(request, default);

        var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("使用者不存在", Assert.IsType<ProblemDetails>(bad.Value).Title);
    }

    // ---------- PUT /api/app-roles ----------

    [Fact]
    public async Task Update_ReadsRoleIdFromBody_AndReturns204()
    {
        var request = Request("Editor");
        _repository.UpdateAsync(request, Arg.Any<CancellationToken>()).Returns(true);

        var result = await _controller.Update(request, default);

        Assert.IsType<NoContentResult>(result);
        await _repository.Received(1).UpdateAsync(
            Arg.Is<AppRoleRequest>(r => r.RoleId == "Editor"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_Returns404_WhenNoRowsAffected()
    {
        var request = Request("Ghost");
        _repository.UpdateAsync(request, Arg.Any<CancellationToken>()).Returns(false);

        var result = await _controller.Update(request, default);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    // ---------- DELETE /api/app-roles/{id} ----------

    [Fact]
    public async Task Delete_Returns204_WhenDeleted()
    {
        _repository.DeleteAsync("Editor", Arg.Any<CancellationToken>()).Returns(true);

        Assert.IsType<NoContentResult>(await _controller.Delete("Editor", default));
    }

    [Fact]
    public async Task Delete_Returns404_WhenMissing()
    {
        _repository.DeleteAsync("Ghost", Arg.Any<CancellationToken>()).Returns(false);

        Assert.IsType<NotFoundObjectResult>(await _controller.Delete("Ghost", default));
    }
}
