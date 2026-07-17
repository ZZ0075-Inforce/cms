using System.Net;
using System.Net.Http.Headers;
using CMS.API.Infrastructure;
using CMS.API.Tests.Infrastructure;
using NSubstitute;

namespace CMS.API.Tests.Controllers;

/// <summary>
/// End-to-end tests for the Admin-only guard on <c>POST /api/app-users/{id}/reset-password</c>
/// (lab05 Step 5). These run through the real JWT middleware via <see cref="AdminAuthTestFactory"/>
/// and are deliberately DB-free (both repositories are substituted), so they run in the default
/// <c>Category!=Integration</c> pass — no <c>[Trait]</c>, no <c>[IntegrationFact]</c>.
/// </summary>
public sealed class AppUsersAuthorizationTests : IClassFixture<AdminAuthTestFactory>
{
    private readonly AdminAuthTestFactory _factory;

    public AppUsersAuthorizationTests(AdminAuthTestFactory factory) => _factory = factory;

    private HttpClient ClientFor(params string[] roles)
    {
        var token = JwtTokenGenerator.Generate(
            AdminAuthTestFactory.SigningKey, "caller", "Caller", roles);
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task ResetPassword_WithoutBearer_Returns401()
    {
        var client = _factory.CreateClient(); // no Authorization header

        var response = await client.PostAsync("/api/app-users/miles@uuu.com.tw/reset-password", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_AsAuthenticatedNonAdmin_Returns403()
    {
        // The substitute is shared by the class fixture; isolate this test's call record.
        _factory.AppUserRepository.ClearReceivedCalls();
        var client = ClientFor("User"); // authenticated, but not Admin

        var response = await client.PostAsync("/api/app-users/miles@uuu.com.tw/reset-password", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        // The action must never run for a non-Admin — authorization short-circuits before the repository.
        await _factory.AppUserRepository.DidNotReceive()
            .ResetPasswordAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResetPassword_AsAdmin_Returns204()
    {
        // The substitute is shared by the class fixture; isolate this test's call record.
        _factory.AppUserRepository.ClearReceivedCalls();
        var client = ClientFor("Admin");

        var response = await client.PostAsync("/api/app-users/miles@uuu.com.tw/reset-password", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await _factory.AppUserRepository.Received(1)
            .ResetPasswordAsync("miles@uuu.com.tw", Arg.Any<CancellationToken>());
    }
}
