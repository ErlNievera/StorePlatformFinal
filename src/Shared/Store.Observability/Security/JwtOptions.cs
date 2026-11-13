using System.ComponentModel.DataAnnotations;

namespace Store.Observability.Security;

/// <summary>
/// JWT settings shared by Identity (issuing) and every API and the gateway (validating), bound
/// from the <c>Jwt</c> section: <c>Jwt__Issuer</c>, <c>Jwt__Audience</c>, <c>Jwt__SigningKey</c>,
/// <c>Jwt__ExpiresMinutes</c> (set from <c>JWT_*</c> in .env).
/// </summary>
public sealed class JwtOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Jwt";

    /// <summary>Token issuer (<c>iss</c>).</summary>
    [Required]
    public string Issuer { get; set; } = string.Empty;

    /// <summary>Token audience (<c>aud</c>).</summary>
    [Required]
    public string Audience { get; set; } = string.Empty;

    /// <summary>HMAC-SHA256 signing key; at least 32 characters.</summary>
    [Required]
    [MinLength(32)]
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>Token lifetime in minutes.</summary>
    [Range(1, 1440)]
    public int ExpiresMinutes { get; set; } = 60;
}
