using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CMS.API.Infrastructure;

/// <summary>
/// Configures JWT bearer validation from the DB-sourced signing key. Runs lazily (options are built on
/// the first authenticated request), so the app boots without a DB round-trip and the key provider is
/// only touched when a token actually needs validating.
///
/// The tokens issued by <see cref="JwtTokenGenerator"/> carry no issuer/audience and use short, unmapped
/// claim names, so issuer/audience validation is off, inbound claim mapping is disabled, and the role /
/// name claim types are pointed at the generator's own constants.
/// </summary>
public sealed class ConfigureJwtBearerOptions(ISigningKeyProvider keyProvider)
    : IConfigureNamedOptions<JwtBearerOptions>
{
    public void Configure(string? name, JwtBearerOptions options)
    {
        // JwtBearerOptions is a named option — this configurator runs for every scheme. Only touch ours.
        if (name != JwtBearerDefaults.AuthenticationScheme)
            return;

        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyProvider.Key)),
            RoleClaimType = JwtTokenGenerator.RoleClaim,
            NameClaimType = JwtTokenGenerator.UserIdClaim
        };
    }

    public void Configure(JwtBearerOptions options) => Configure(Options.DefaultName, options);
}
