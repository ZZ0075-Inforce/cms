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
/// <c>[Authorize(Roles = "Admin")]</c> gates on the three admin controllers) with the repositories
/// those controllers touch substituted — so role-based authorization is exercised end to end without
/// a database. The signing key is served from the substituted <see cref="IAuthRepository"/>, exactly
/// where the lazy provider reads it.
///
/// This must go through <see cref="WebApplicationFactory{T}"/> and cannot be a plain controller unit
/// test: <c>[Authorize]</c> is an MVC filter evaluated by the pipeline, so a directly-constructed
/// controller never runs one and would pass whether the attribute were present, absent or misspelled.
/// That blind spot is exactly how the role gates came to be missing in the first place.
///
/// Substitutes report success for the actions these tests drive, so any call that gets PAST
/// authorization reaches a 2xx — which keeps "403 vs 204" a statement about the gate and nothing else.
/// </summary>
public sealed class AdminAuthTestFactory : WebApplicationFactory<Program>
{
    // HS256 needs at least 256 bits (32 UTF-8 bytes); this is comfortably longer.
    public const string SigningKey = "app-users-auth-integration-signing-key-0123456789-abcdef";

    public IAuthRepository AuthRepository { get; } = Substitute.For<IAuthRepository>();
    public IAppUserRepository AppUserRepository { get; } = Substitute.For<IAppUserRepository>();
    public IAppRoleRepository AppRoleRepository { get; } = Substitute.For<IAppRoleRepository>();
    public IPublishStatusRepository PublishStatusRepository { get; } =
        Substitute.For<IPublishStatusRepository>();

    public AdminAuthTestFactory()
    {
        AuthRepository.GetSigningKeyAsync(Arg.Any<CancellationToken>()).Returns(SigningKey);

        // Anything that gets past authorization finds its row and succeeds.
        AppUserRepository.ResetPasswordAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);
        AppUserRepository.UpdateAsync(Arg.Any<Models.AppUserRequest>(), Arg.Any<CancellationToken>())
            .Returns(true);
        AppRoleRepository.UpdateAsync(Arg.Any<Models.AppRoleRequest>(), Arg.Any<CancellationToken>())
            .Returns(true);
    }

    /// <summary>Clears the call record on every substitute — the fixture is shared across a class.</summary>
    public void ClearAllReceivedCalls()
    {
        AppUserRepository.ClearReceivedCalls();
        AppRoleRepository.ClearReceivedCalls();
        PublishStatusRepository.ClearReceivedCalls();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IAuthRepository>();
            services.AddScoped(_ => AuthRepository);

            services.RemoveAll<IAppUserRepository>();
            services.AddScoped(_ => AppUserRepository);

            services.RemoveAll<IAppRoleRepository>();
            services.AddScoped(_ => AppRoleRepository);

            services.RemoveAll<IPublishStatusRepository>();
            services.AddScoped(_ => PublishStatusRepository);
        });
    }
}
