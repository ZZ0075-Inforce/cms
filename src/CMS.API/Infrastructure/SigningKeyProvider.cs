using CMS.API.Repositories;

namespace CMS.API.Infrastructure;

/// <summary>
/// Supplies the JWT signing secret to the bearer-validation options. The secret lives in the DB
/// (SysConfig 'appConfig'), not appsettings, so it cannot be a static startup value; this provider
/// reads it once — lazily, on the first authenticated request — and caches it for the process.
/// </summary>
public interface ISigningKeyProvider
{
    /// <summary>The symmetric HS256 secret, read from SysConfig on first use and cached thereafter.</summary>
    string Key { get; }
}

/// <summary>
/// Caches the signing key read via <see cref="IAuthRepository.GetSigningKeyAsync"/>. Registered as a
/// singleton, so it must resolve the <em>scoped</em> repository through <see cref="IServiceScopeFactory"/>
/// rather than capturing it (a captured scoped dependency is a captive-dependency bug).
/// </summary>
public sealed class SigningKeyProvider(IServiceScopeFactory scopeFactory) : ISigningKeyProvider
{
    private readonly Lazy<string> _key = new(() =>
    {
        using var scope = scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAuthRepository>();
        // Sync-over-async is acceptable here: it runs once, the first time a token is validated.
        return repository.GetSigningKeyAsync().GetAwaiter().GetResult();
    });

    public string Key => _key.Value;
}
