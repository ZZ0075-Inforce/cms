using CMS.API.Repositories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;

namespace CMS.API.Tests.Infrastructure;

/// <summary>
/// Boots the real API pipeline (including the global exception middleware, auth and the fallback
/// policy) with <see cref="TestDiagnosticsController"/> added as an application part, so the
/// exception-handling tests have throw / forbidden / validate endpoints without a database. The
/// DB-sourced signing key is served from a substituted <see cref="IAuthRepository"/>.
/// </summary>
public sealed class ExceptionHandlingTestFactory : WebApplicationFactory<Program>
{
    public const string SigningKey = "exception-handling-integration-signing-key-0123456789-abcdef";

    public IAuthRepository AuthRepository { get; } = Substitute.For<IAuthRepository>();

    public ExceptionHandlingTestFactory() =>
        AuthRepository.GetSigningKeyAsync(Arg.Any<CancellationToken>()).Returns(SigningKey);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IAuthRepository>();
            services.AddScoped(_ => AuthRepository);

            // Make the test-only controller discoverable by the real MVC pipeline.
            services.AddControllers().AddApplicationPart(typeof(TestDiagnosticsController).Assembly);
        });
    }
}
