using System.Security.Claims;
using CMS.API.Controllers;
using CMS.API.Infrastructure;
using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;

namespace CMS.API.Tests.Controllers;

/// <summary>
/// Unit tests for PUT /api/Auth/profile. The repository is mocked, so these never touch SQL: they pin
/// the "rename only the JWT user" contract — the UserId always comes from the token claim, never the
/// request — and the required/trimmed UserName validation. The body carries no UserId field at all
/// (<see cref="UpdateProfileRequest"/>), so it cannot target another account; the end-to-end proof that
/// a UserId smuggled into the raw JSON is ignored lives in AuthProfileEndpointTests.
/// </summary>
public class AuthProfileControllerTests
{
    private readonly IAuthRepository _repository = Substitute.For<IAuthRepository>();

    private AuthController ControllerFor(string? userId)
    {
        var claims = userId is null
            ? Array.Empty<Claim>()
            : new[] { new Claim(JwtTokenGenerator.UserIdClaim, userId) };

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Test"));

        return new AuthController(_repository)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal }
            }
        };
    }

    private static UpdateProfileRequest Request(string userName) => new() { UserName = userName };

    [Fact]
    public async Task UpdateProfile_UpdatesUserName_ForTheJwtUser()
    {
        _repository.UpdateUserNameAsync("admin", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);
        var controller = ControllerFor("admin");

        var result = await controller.UpdateProfile(Request("新名字"), default);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<ProfileResponse>(ok.Value);
        Assert.Equal("admin", body.UserId);
        Assert.Equal("新名字", body.UserName);

        await _repository.Received(1).UpdateUserNameAsync("admin", "新名字", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateProfile_TrimsUserName_BeforeSaving()
    {
        _repository.UpdateUserNameAsync("admin", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);
        var controller = ControllerFor("admin");

        var result = await controller.UpdateProfile(Request("  新名字  "), default);

        var body = Assert.IsType<ProfileResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal("新名字", body.UserName);
        await _repository.Received(1).UpdateUserNameAsync("admin", "新名字", Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public async Task UpdateProfile_Rejects_EmptyOrWhitespaceUserName(string userName)
    {
        var controller = ControllerFor("admin");

        var result = await controller.UpdateProfile(Request(userName), default);

        var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.IsType<ProblemDetails>(bad.Value);
        // A blank name is never written.
        await _repository.DidNotReceive()
            .UpdateUserNameAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateProfile_Returns401_WhenTokenHasNoUserIdClaim()
    {
        var controller = ControllerFor(userId: null);

        var result = await controller.UpdateProfile(Request("新名字"), default);

        Assert.IsType<UnauthorizedResult>(result.Result);
        await _repository.DidNotReceive()
            .UpdateUserNameAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateProfile_Returns404_WhenNoRowMatches()
    {
        // Row missing (e.g. the account was deleted after the token was issued).
        _repository.UpdateUserNameAsync("ghost", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);
        var controller = ControllerFor("ghost");

        var result = await controller.UpdateProfile(Request("新名字"), default);

        Assert.IsType<NotFoundResult>(result.Result);
    }
}
