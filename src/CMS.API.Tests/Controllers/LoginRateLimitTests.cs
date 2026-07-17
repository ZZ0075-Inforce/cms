using System.Net;
using System.Net.Http.Json;
using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;

namespace CMS.API.Tests.Controllers;

/// <summary>
/// Proves the login rate limiter is actually wired into the pipeline. This is worth a test precisely
/// because the failure mode is silent: if <c>UseRateLimiter</c> ran before <c>UseRouting</c>, the
/// endpoint-specific policy would never resolve and login would go unthrottled with no error anywhere.
/// A limiter you believe in but never exercised is not a limiter.
///
/// Its own factory, not <see cref="Infrastructure.AdminAuthTestFactory"/>: the limiter holds
/// per-partition state on the server, so a fixture shared with other tests would leak a consumed
/// budget into them (and their traffic into this one's budget).
/// </summary>
public sealed class LoginRateLimitTests : IClassFixture<LoginRateLimitTests.Factory>
{
    /// <summary>Matches PermitLimit in Program.cs.</summary>
    private const int PermitLimit = 10;

    private readonly Factory _factory;

    public LoginRateLimitTests(Factory factory) => _factory = factory;

    public sealed class Factory : WebApplicationFactory<Program>
    {
        // HS256 needs at least 256 bits (32 UTF-8 bytes). Required even though no test here logs in
        // successfully: JwtBearer builds its SymmetricSecurityKey when the FIRST request hits the
        // authentication middleware, so an unstubbed key throws before login is ever reached.
        private const string SigningKey = "login-rate-limit-test-signing-key-0123456789-abcdef";

        public IAuthRepository AuthRepository { get; } = Substitute.For<IAuthRepository>();

        public Factory()
        {
            AuthRepository.GetSigningKeyAsync(Arg.Any<CancellationToken>()).Returns(SigningKey);
            // Every attempt is a failed login: the limiter must count attempts, not just successes,
            // and must not need a database.
            AuthRepository.AuthenticateAsync(
                    Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns((AuthenticatedUser?)null);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder) =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IAuthRepository>();
                services.AddScoped(_ => AuthRepository);
            });
    }

    [Fact]
    public async Task Login_IsThrottled_OnceThePermitLimitIsExhausted()
    {
        var client = _factory.CreateClient();
        var body = new LoginRequest { UserId = "nobody", Password = "wrong" };

        // Burn the window's budget. Every one of these is a normal 401 — the limiter counts attempts.
        for (var i = 0; i < PermitLimit; i++)
        {
            var allowed = await client.PostAsJsonAsync("/api/Auth/login", body);
            Assert.Equal(HttpStatusCode.Unauthorized, allowed.StatusCode);
        }

        var throttled = await client.PostAsJsonAsync("/api/Auth/login", body);

        // 429, not 401: the caller is being told to slow down, which says nothing about the account.
        Assert.Equal(HttpStatusCode.TooManyRequests, throttled.StatusCode);

        // The rejection must carry a body. Without one the Angular login page falls back to
        // "使用者代碼或密碼錯誤。" and tells a throttled user their password is wrong — so an empty
        // 429 is a real bug, not a cosmetic one.
        var problem = await throttled.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("嘗試次數過多", problem.Title);
        Assert.False(string.IsNullOrWhiteSpace(problem.Detail));
    }
}
