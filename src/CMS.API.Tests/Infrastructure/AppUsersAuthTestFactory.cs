using CMS.API.Repositories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;

namespace CMS.API.Tests.Infrastructure;

/// <summary>
/// Boots the real API pipeline (auth middleware, fallback policy, JwtBearer options, and the
/// <c>[Authorize(Roles = "Admin")]</c> on reset-password) with the two repositories these tests touch
/// substituted — so role-based authorization is exercised end to end without a database. The signing
/// key is served from the substituted <see cref="IAuthRepository"/>, exactly where the lazy provider
/// reads it; <see cref="IAppUserRepository"/> reports the reset as succeeded so an authorized call can
/// reach 204.
/// </summary>
public sealed class AppUsersAuthTestFactory : WebApplicationFactory<Program>
{
    // HS256 needs at least 256 bits (32 UTF-8 bytes); this is comfortably longer.
    public const string SigningKey = "app-users-auth-integration-signing-key-0123456789-abcdef";

    public IAuthRepository AuthRepository { get; } = Substitute.For<IAuthRepository>();
    public IAppUserRepository AppUserRepository { get; } = Substitute.For<IAppUserRepository>();

    public AppUsersAuthTestFactory()
    {
        AuthRepository.GetSigningKeyAsync(Arg.Any<CancellationToken>()).Returns(SigningKey);
        // Any reset that gets past authorization finds its user and succeeds → 204.
        AppUserRepository.ResetPasswordAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IAuthRepository>();
            services.AddScoped(_ => AuthRepository);

            services.RemoveAll<IAppUserRepository>();
            services.AddScoped(_ => AppUserRepository);
        });
    }
}
