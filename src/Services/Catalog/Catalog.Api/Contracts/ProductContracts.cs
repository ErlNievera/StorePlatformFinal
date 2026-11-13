using System.ComponentModel.DataAnnotations;
using Catalog.Api.Domain;

namespace Catalog.Api.Contracts;

/// <summary>Product as returned by the API (schema <c>Product</c> in catalog.v1.yaml).</summary>
public sealed record ProductResponse(
    Guid ProductId,
    string Name,
    string Description,
    decimal Price,
    string Currency,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    /// <summary>Maps an entity.</summary>
    public static ProductResponse From(Product product) => new(
        product.ProductId,
        product.Name,
        product.Description,
        product.Price,
        product.Currency,
        product.IsActive,
        product.CreatedAt,
        product.UpdatedAt);
}

/// <summary>Create or update body (schema <c>ProductRequest</c>).</summary>
public sealed class ProductRequest
{
    /// <summary>Display name, unique across products.</summary>
    [Required]
    [StringLength(Product.NameMaxLength, MinimumLength = 2)]
    public string Name { get; init; } = string.Empty;

    /// <summary>Description shown on the product page.</summary>
    [Required(AllowEmptyStrings = true)]
    [StringLength(Product.DescriptionMaxLength)]
    public string Description { get; init; } = string.Empty;

    /// <summary>Unit price.</summary>
    [Range(typeof(decimal), "0.01", "1000000")]
    public decimal Price { get; init; }

    /// <summary>ISO 4217 code; defaults to PHP.</summary>
    [StringLength(3, MinimumLength = 3)]
    [RegularExpression("^[A-Za-z]{3}$", ErrorMessage = "Currency must be a 3-letter ISO 4217 code.")]
    public string? Currency { get; init; }

    /// <summary>Whether the product can be browsed and ordered.</summary>
    [Required]
    public bool? IsActive { get; init; }
}

/// <summary>Fault-injection settings (schema <c>ChaosSettings</c>).</summary>
public sealed class ChaosSettingsDto
{
    /// <summary>Share of product requests answered with 503 (0 to 1).</summary>
    [Range(0d, 1d)]
    public double FailureRate { get; init; }

    /// <summary>Latency added to every product request.</summary>
    [Range(0, 30000)]
    public int DelayMs { get; init; }
}
