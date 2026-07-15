using System.Text.Json;
using CMS.API.Controllers;
using CMS.API.Infrastructure;
using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;
using NSubstitute;

namespace CMS.API.Tests.Controllers;

/// <summary>
/// HTTP-contract tests for POST /api/Auth/login. The repository is mocked, so these never touch SQL:
/// they pin the 401-vs-200 contract, the JWT's claims and lifetime, and the "no PasswordHash in the
/// response" guarantee. The credential matrix itself (wrong password / unknown user / inactive each
/// producing a null result) is proven against the real DB in AuthRepositoryIntegrationTests.
/// </summary>
public class AuthControllerTests
{
    // HS256 needs a key of at least 256 bits (32 UTF-8 bytes); this one is comfortably longer.
    private const string SigningKey = "unit-test-signing-key-0123456789-abcdefghijklmnop";

    private readonly IAuthRepository _repository = Substitute.For<IAuthRepository>();
    private readonly AuthController _controller;

    public AuthControllerTests() => _controller = new AuthController(_repository);

    private static LoginRequest Request(string userId = "admin", string password = "correct-horse")
        => new() { UserId = userId, Password = password };

    private void ArrangeValidUser(
        string userId = "admin", string userName = "Administrator", params string[] roleIds)
    {
        _repository.AuthenticateAsync(userId, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new AuthenticatedUser(userId, userName, roleIds));
        _repository.GetSigningKeyAsync(Arg.Any<CancellationToken>()).Returns(SigningKey);
    }

    // ---------- Success ----------

    [Fact]
    public async Task Login_ReturnsOk_WithProfileAndToken_ForValidUser()
    {
        ArrangeValidUser("admin", "Administrator", "Admin", "Editor");

        var result = await _controller.Login(Request("admin"), default);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<LoginResponse>(ok.Value);
        Assert.Equal("admin", body.UserId);
        Assert.Equal("Administrator", body.UserName);
        Assert.False(string.IsNullOrWhiteSpace(body.AccessToken));
    }

    [Fact]
    public async Task Login_IssuedToken_CarriesUserAndRoleClaims()
    {
        ArrangeValidUser("admin", "Administrator", "Admin", "Editor");

        var result = await _controller.Login(Request("admin"), default);

        var body = Assert.IsType<LoginResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        var jwt = new JsonWebToken(body.AccessToken);

        Assert.Equal("admin", jwt.GetClaim(JwtTokenGenerator.UserIdClaim).Value);
        Assert.Equal("Administrator", jwt.GetClaim(JwtTokenGenerator.UserNameClaim).Value);

        var roles = jwt.Claims.Where(c => c.Type == JwtTokenGenerator.RoleClaim).Select(c => c.Value);
        Assert.Equal(new[] { "Admin", "Editor" }.OrderBy(x => x), roles.OrderBy(x => x));
    }

    [Fact]
    public async Task Login_IssuedToken_ExpiresInAbout24Hours()
    {
        ArrangeValidUser();

        var result = await _controller.Login(Request(), default);

        var body = Assert.IsType<LoginResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        var jwt = new JsonWebToken(body.AccessToken);

        // ValidTo/ValidFrom are UTC. Lifetime is 24h; allow a few minutes for clock/execution slack.
        Assert.Equal(24, (jwt.ValidTo - jwt.ValidFrom).TotalHours, precision: 2);
        Assert.True(
            Math.Abs((jwt.ValidTo - DateTime.UtcNow - TimeSpan.FromHours(24)).TotalMinutes) < 5,
            $"expiry {jwt.ValidTo:o} is not ~24h from now");
    }

    // ---------- Failure ----------

    [Theory]
    [InlineData("wrong password")]
    [InlineData("unknown UserId")]
    [InlineData("inactive account (IsActive = 0)")]
    public async Task Login_Returns401_WhenCredentialsRejected(string scenario)
    {
        // The repository collapses all three failure modes into a single null — the endpoint must not
        // reveal which one occurred (scenario is only a label for readability).
        _ = scenario;
        _repository.AuthenticateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((AuthenticatedUser?)null);

        var result = await _controller.Login(Request(), default);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        var problem = Assert.IsType<ProblemDetails>(unauthorized.Value);
        Assert.Equal("登入失敗", problem.Title);
    }

    [Fact]
    public async Task Login_WhenRejected_DoesNotReadSigningKeyOrIssueToken()
    {
        _repository.AuthenticateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((AuthenticatedUser?)null);

        await _controller.Login(Request(), default);

        await _repository.DidNotReceive().GetSigningKeyAsync(Arg.Any<CancellationToken>());
    }

    // ---------- PasswordHash never leaves the boundary ----------

    [Fact]
    public async Task Login_Response_NeverExposesPasswordHash()
    {
        ArrangeValidUser("admin", "Administrator", "Admin");

        var result = await _controller.Login(Request("admin"), default);
        var body = Assert.IsType<LoginResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);

        // Serialize exactly as the API would (ASP.NET Core uses camelCase Web defaults), then
        // inspect the wire shape.
        var json = JsonSerializer.Serialize(body, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        using var doc = JsonDocument.Parse(json);
        var keys = doc.RootElement.EnumerateObject().Select(p => p.Name).ToList();

        Assert.Equal(new[] { "userId", "userName", "accessToken" }.OrderBy(x => x), keys.OrderBy(x => x));
        Assert.DoesNotContain(keys, k => k.Contains("password", StringComparison.OrdinalIgnoreCase));
    }
}
