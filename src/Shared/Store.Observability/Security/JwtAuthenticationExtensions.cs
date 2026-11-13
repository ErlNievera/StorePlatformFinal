using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Store.Observability.Security;

/// <summary>JWT bearer validation used by every service, so <c>[Authorize(Roles = ...)]</c> works behind the gateway.</summary>
public static class JwtAuthenticationExtensions
{
    /// <summary>
    /// Binds and validates <see cref="JwtOptions"/>, and adds JWT bearer authentication that checks
    /// issuer, audience, lifetime and the HMAC signature. Inbound claim mapping is off, so roles are
    /// read from the short <c>role</c> claim.
    /// </summary>
    public static IServiceCollection AddStoreJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = CreateValidationParameters(jwt);
            });
        services.AddAuthorization();
        return services;
    }

    /// <summary>Validation parameters matching tokens issued by Identity.</summary>
    public static TokenValidationParameters CreateValidationParameters(JwtOptions jwt)
    {
        ArgumentNullException.ThrowIfNull(jwt);
        return new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = CreateSigningKey(jwt.SigningKey),
            NameClaimType = StoreClaims.Name,
            RoleClaimType = StoreClaims.Role,
        };
    }

    /// <summary>The symmetric key for HMAC-SHA256 signing and validation.</summary>
    public static SymmetricSecurityKey CreateSigningKey(string signingKey) =>
        new(Encoding.UTF8.GetBytes(signingKey ?? string.Empty));
}
