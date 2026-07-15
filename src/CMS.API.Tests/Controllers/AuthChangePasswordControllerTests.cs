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
/// Unit tests for POST /api/Auth/change-password. The repository is mocked, so these never touch SQL:
/// they pin the ordered contract — the current password must verify first (a wrong one changes nothing),
/// then the new password must meet the complexity policy, then new/confirm must match — and that only a
/// fully-valid request calls UpdatePasswordAsync (with the UserId taken from the JWT). The actual
/// "PasswordHash = SHA256(new) + PasswordUpdatedTime bumped" write is proven against the real DB in
/// AuthRepositoryIntegrationTests.
/// </summary>
public class AuthChangePasswordControllerTests
{
    private const string CurrentPassword = "OldPass#1";
    // Length >= 8 with upper + lower + digit + symbol — comfortably complex.
    private const string StrongNewPassword = "NewPass#2";

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

    private void ArrangeCurrentPasswordValid(string userId = "admin")
    {
        _repository.VerifyPasswordAsync(userId, CurrentPassword, Arg.Any<CancellationToken>())
            .Returns(true);
        _repository.UpdatePasswordAsync(userId, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);
    }

    private static ChangePasswordRequest Request(
        string current = CurrentPassword, string @new = StrongNewPassword, string? confirm = null)
        => new() { CurrentPassword = current, NewPassword = @new, ConfirmPassword = confirm ?? @new };

    // ---------- Success ----------

    [Fact]
    public async Task ChangePassword_ValidRequest_UpdatesPasswordForJwtUser_AndReturns204()
    {
        ArrangeCurrentPasswordValid("admin");
        var controller = ControllerFor("admin");

        var result = await controller.ChangePassword(Request(), default);

        Assert.IsType<NoContentResult>(result);
        await _repository.Received(1)
            .UpdatePasswordAsync("admin", StrongNewPassword, Arg.Any<CancellationToken>());
    }

    // ---------- Wrong current password ----------

    [Fact]
    public async Task ChangePassword_WrongCurrentPassword_ChangesNothing()
    {
        // Verify fails → the endpoint must not persist anything.
        _repository.VerifyPasswordAsync("admin", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);
        var controller = ControllerFor("admin");

        var result = await controller.ChangePassword(
            Request(current: "wrong-current"), default);

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.IsType<ProblemDetails>(bad.Value);
        await _repository.DidNotReceive()
            .UpdatePasswordAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // ---------- Complexity ----------

    [Theory]
    [InlineData("Ab1!")]           // too short
    [InlineData("abcdefgh")]       // long enough but only one class
    [InlineData("abcdefgh12345")]  // only two classes
    public async Task ChangePassword_WeakNewPassword_Rejected_WithComplexityMessage(string weak)
    {
        ArrangeCurrentPasswordValid("admin");
        var controller = ControllerFor("admin");

        var result = await controller.ChangePassword(
            Request(@new: weak, confirm: weak), default);

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(bad.Value);
        Assert.Equal(PasswordPolicy.ViolationMessage, problem.Detail);
        await _repository.DidNotReceive()
            .UpdatePasswordAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // ---------- New / confirm mismatch ----------

    [Fact]
    public async Task ChangePassword_NewAndConfirmMismatch_Rejected()
    {
        ArrangeCurrentPasswordValid("admin");
        var controller = ControllerFor("admin");

        // Both are complex, but the confirmation differs.
        var result = await controller.ChangePassword(
            Request(@new: StrongNewPassword, confirm: "Other#Pass3"), default);

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.IsType<ProblemDetails>(bad.Value);
        await _repository.DidNotReceive()
            .UpdatePasswordAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // ---------- No identity ----------

    [Fact]
    public async Task ChangePassword_Returns401_WhenTokenHasNoUserIdClaim()
    {
        var controller = ControllerFor(userId: null);

        var result = await controller.ChangePassword(Request(), default);

        Assert.IsType<UnauthorizedResult>(result);
        await _repository.DidNotReceive()
            .VerifyPasswordAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _repository.DidNotReceive()
            .UpdatePasswordAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
