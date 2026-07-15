using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CMS.API.Infrastructure;
using CMS.API.Models;
using CMS.API.Tests.Infrastructure;
using NSubstitute;

namespace CMS.API.Tests.Controllers;

/// <summary>
/// End-to-end tests for PUT /api/Auth/profile through the real JWT pipeline (via
/// <see cref="JwtAuthTestFactory"/>, DB-free). They prove the endpoint is protected, that the UserId is
/// taken from the token and a UserId placed in the request body is ignored, and that a whitespace name
/// is rejected — the guarantees the task hinges on, verified over the wire rather than in isolation.
/// </summary>
public sealed class AuthProfileEndpointTests : IClassFixture<JwtAuthTestFactory>
{
    private readonly JwtAuthTestFactory _factory;

    public AuthProfileEndpointTests(JwtAuthTestFactory factory) => _factory = factory;

    private static string TokenFor(string userId) =>
        JwtTokenGenerator.Generate(JwtAuthTestFactory.SigningKey, userId, "任何名字", new[] { "Admin" });

    private HttpClient ClientFor(string userId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TokenFor(userId));
        return client;
    }

    [Fact]
    public async Task UpdateProfile_TakesUserIdFromToken_IgnoringBodyUserId()
    {
        _factory.AuthRepository.ClearReceivedCalls();
        _factory.AuthRepository
            .UpdateUserNameAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var client = ClientFor("admin");

        // The body tries to point the update at a different account — it must be ignored.
        var response = await client.PutAsJsonAsync(
            "/api/Auth/profile", new { userId = "someone-else", userName = "新名字" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ProfileResponse>();
        Assert.NotNull(body);
        Assert.Equal("admin", body!.UserId);
        Assert.Equal("新名字", body.UserName);

        // The repository was told to rename the token's user, never the body's.
        await _factory.AuthRepository.Received(1)
            .UpdateUserNameAsync("admin", "新名字", Arg.Any<CancellationToken>());
        await _factory.AuthRepository.DidNotReceive()
            .UpdateUserNameAsync("someone-else", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateProfile_WithoutBearer_Returns401()
    {
        // No Authorization header — the [Authorize] action must not be reachable anonymously, proving
        // the class-level [AllowAnonymous] on login does not leak to this action.
        var client = _factory.CreateClient();

        var response = await client.PutAsJsonAsync(
            "/api/Auth/profile", new { userName = "新名字" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UpdateProfile_WithWhitespaceUserName_Returns400_AndDoesNotWrite()
    {
        _factory.AuthRepository.ClearReceivedCalls();
        var client = ClientFor("admin");

        var response = await client.PutAsJsonAsync(
            "/api/Auth/profile", new { userName = "   " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await _factory.AuthRepository.DidNotReceive()
            .UpdateUserNameAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
