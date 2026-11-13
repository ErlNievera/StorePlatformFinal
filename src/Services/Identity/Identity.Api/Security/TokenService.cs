using System.Security.Claims;
using Identity.Api.Domain;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Store.Observability.Security;

namespace Identity.Api.Security;

/// <summary>Issues the HMAC-SHA256 JWTs that the gateway and every service validate.</summary>
public sealed class TokenService(IOptions<JwtOptions> options, TimeProvider clock)
{
    private readonly JsonWebTokenHandler _handler = new() { SetDefaultTimesOnTokenCreation = false };

    /// <summary>Creates a token for <paramref name="user"/> with one <c>role</c> claim per role.</summary>
    public (string Token, DateTimeOffset ExpiresAt) CreateToken(AppUser user, IEnumerable<string> roles)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(roles);

        var jwt = options.Value;
        var now = clock.GetUtcNow();
        var expiresAt = now.AddMinutes(jwt.ExpiresMinutes);

        var claims = new List<Claim>
        {
            new(StoreClaims.Subject, user.Id.ToString()),
            new(StoreClaims.Email, user.Email ?? string.Empty),
            new(StoreClaims.Name, user.FullName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
        };
        claims.AddRange(roles.Select(role => new Claim(StoreClaims.Role, role)));

        var token = _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            Subject = new ClaimsIdentity(claims),
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = new SigningCredentials(
                JwtAuthenticationExtensions.CreateSigningKey(jwt.SigningKey),
                SecurityAlgorithms.HmacSha256),
        });

        return (token, expiresAt);
    }
}
