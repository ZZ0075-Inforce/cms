using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CMS.API.Infrastructure;
using CMS.API.Models;
using CMS.API.Tests.Infrastructure;
using NSubstitute;

namespace CMS.API.Tests.Controllers;

/// <summary>
/// End-to-end tests for the Admin-only guards on the three 系統管理 controllers, through the real JWT
/// middleware via <see cref="AdminAuthTestFactory"/>. DB-free (every repository is substituted), so
/// these run in the default <c>Category!=Integration</c> pass.
///
/// The privilege-escalation tests below are the point of this file. AppUserRequest.RoleIds and
/// AppRoleRequest.UserIds are synced straight into AppUserRole, and the JWT mints one `role` claim per
/// row — so before these gates existed, ANY authenticated caller could PUT itself `Admin` and re-login
/// as an administrator. They are written as the attack, not as a status-code check, so a future
/// refactor that removes a gate fails with an obvious name.
/// </summary>
public sealed class AdminAuthorizationTests : IClassFixture<AdminAuthTestFactory>
{
    private readonly AdminAuthTestFactory _factory;

    public AdminAuthorizationTests(AdminAuthTestFactory factory) => _factory = factory;

    private HttpClient ClientFor(params string[] roles)
    {
        var token = JwtTokenGenerator.Generate(
            AdminAuthTestFactory.SigningKey, "caller", "Caller", roles);
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    // ---------- Privilege escalation: granting yourself Admin ----------

    [Fact]
    public async Task UpdateAppUser_AsNonAdmin_CannotGrantItselfTheAdminRole()
    {
        _factory.ClearAllReceivedCalls();
        var client = ClientFor("User"); // authenticated, but not Admin

        // The attack: name yourself, hand yourself Admin.
        var response = await client.PutAsJsonAsync("/api/app-users", new AppUserRequest
        {
            UserId = "caller",
            UserName = "Caller",
            RoleIds = ["Admin"]
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        // The gate must short-circuit before the repository — no AppUserRole row may ever be written.
        await _factory.AppUserRepository.DidNotReceive()
            .UpdateAsync(Arg.Any<AppUserRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAppRole_AsNonAdmin_CannotAddItselfToTheAdminRole()
    {
        _factory.ClearAllReceivedCalls();
        var client = ClientFor("User");

        // The same escalation from the other side — and the n-n sync is delete-then-reinsert, so this
        // payload would ALSO unassign every existing Admin.
        var response = await client.PutAsJsonAsync("/api/app-roles", new AppRoleRequest
        {
            RoleId = "Admin",
            Description = "Admin",
            UserIds = ["caller"]
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await _factory.AppRoleRepository.DidNotReceive()
            .UpdateAsync(Arg.Any<AppRoleRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAppUser_AsNonAdmin_CannotMintAnAdminAccount()
    {
        _factory.ClearAllReceivedCalls();
        var client = ClientFor("User");

        // Create carries RoleIds too — gating only Update would leave create-an-admin open.
        var response = await client.PostAsJsonAsync("/api/app-users", new AppUserRequest
        {
            UserId = "backdoor@example.com",
            UserName = "backdoor",
            RoleIds = ["Admin"]
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await _factory.AppUserRepository.DidNotReceive()
            .InsertAsync(Arg.Any<AppUserRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAppUser_AsAdmin_IsAllowedThrough()
    {
        _factory.ClearAllReceivedCalls();
        var client = ClientFor("Admin");

        var response = await client.PutAsJsonAsync("/api/app-users", new AppUserRequest
        {
            UserId = "caller",
            UserName = "Caller",
            RoleIds = ["Admin"]
        });

        // Proves the gate rejects on ROLE, not on the payload shape — otherwise the 403s above
        // would pass for the wrong reason.
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await _factory.AppUserRepository.Received(1)
            .UpdateAsync(Arg.Any<AppUserRequest>(), Arg.Any<CancellationToken>());
    }

    // ---------- The admin areas are closed to non-Admins, reads included ----------

    [Theory]
    [InlineData("/api/app-users")]
    [InlineData("/api/app-roles")]
    [InlineData("/api/publish-statuses")]
    public async Task AdminAreaList_AsNonAdmin_Returns403(string url)
    {
        var client = ClientFor("User");

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/app-users")]
    [InlineData("/api/app-roles")]
    [InlineData("/api/publish-statuses")]
    public async Task AdminAreaList_WithoutBearer_Returns401(string url)
    {
        var client = _factory.CreateClient(); // no Authorization header

        var response = await client.GetAsync(url);

        // 401 not 403: unauthenticated is a different answer from unauthorized.
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// The Course form's 上架狀態 dropdown reads lookups, not the admin CRUD controller. Gating
    /// PublishStatusesController must not starve it — a non-Admin still has to be able to pick a status.
    /// </summary>
    [Fact]
    public async Task PublishStatusLookup_StaysOpenToNonAdmins()
    {
        var client = ClientFor("User");

        var response = await client.GetAsync("/api/lookups/publish-statuses");

        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
