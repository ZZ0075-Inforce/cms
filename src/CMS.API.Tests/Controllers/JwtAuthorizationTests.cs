using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CMS.API.Infrastructure;
using CMS.API.Models;
using CMS.API.Tests.Infrastructure;
using NSubstitute;

namespace CMS.API.Tests.Controllers;

/// <summary>
/// End-to-end tests for the JWT bearer pipeline: the fallback policy protects every controller, a valid
/// token unlocks it, and AuthController stays anonymous. These run through the real middleware via
/// <see cref="JwtAuthTestFactory"/> and are deliberately DB-free (both repositories are substituted), so
/// they run in the default <c>Category!=Integration</c> pass — no <c>[Trait]</c>, no <c>[IntegrationFact]</c>.
/// </summary>
public sealed class JwtAuthorizationTests : IClassFixture<JwtAuthTestFactory>
{
    private readonly JwtAuthTestFactory _factory;

    public JwtAuthorizationTests(JwtAuthTestFactory factory) => _factory = factory;

    [Fact]
    public async Task ProtectedEndpoint_WithoutBearer_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/partners");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithValidBearer_Returns200()
    {
        var token = JwtTokenGenerator.Generate(
            JwtAuthTestFactory.SigningKey, "admin", "Administrator", new[] { "Admin" });

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/partners");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AuthController_StaysAnonymous_LoginReachableWithoutToken()
    {
        // Arrange a valid login so the anonymous request reaches the controller and returns 200 —
        // proving [AllowAnonymous] exempts Auth from the RequireAuthenticatedUser fallback policy.
        _factory.AuthRepository
            .AuthenticateAsync("admin", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new AuthenticatedUser("admin", "Administrator", new[] { "Admin" }));

        var client = _factory.CreateClient(); // no Authorization header

        var response = await client.PostAsJsonAsync(
            "/api/Auth/login", new { userId = "admin", password = "irrelevant" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
