using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;

namespace CMS.API.Tests.Infrastructure;

/// <summary>
/// Boots the real API pipeline (auth middleware, fallback policy, JwtBearer options) in memory, with
/// the two repositories the auth tests touch replaced by substitutes — so the tests exercise real
/// authentication end to end without a database. The DB-sourced signing key is served from the
/// substituted <see cref="IAuthRepository"/>, which is exactly where the lazy provider reads it.
/// </summary>
public sealed class JwtAuthTestFactory : WebApplicationFactory<Program>
{
    // HS256 needs at least 256 bits (32 UTF-8 bytes); this is comfortably longer.
    public const string SigningKey = "jwt-auth-integration-signing-key-0123456789-abcdef";

    public IAuthRepository AuthRepository { get; } = Substitute.For<IAuthRepository>();
    public IPartnerRepository PartnerRepository { get; } = Substitute.For<IPartnerRepository>();

    public JwtAuthTestFactory()
    {
        // The signing key the validation options will read (no DB), and an empty partner list so a
        // protected GET can succeed without touching SQL.
        AuthRepository.GetSigningKeyAsync(Arg.Any<CancellationToken>()).Returns(SigningKey);
        PartnerRepository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Partner>)new List<Partner>());
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IAuthRepository>();
            services.AddScoped(_ => AuthRepository);

            services.RemoveAll<IPartnerRepository>();
            services.AddScoped(_ => PartnerRepository);
        });
    }
}
