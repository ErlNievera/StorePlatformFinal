using System.ComponentModel.DataAnnotations;
using Storefront.Web.Clients.Catalog;
using Storefront.Web.Clients.Orders;
using Storefront.Web.Services;

namespace Storefront.Web.Models;

/// <summary>A product card on the catalog grid.</summary>
public sealed record ProductCard(Product Product, int? Available)
{
    /// <summary>True when stock is known and at least one unit can be ordered.</summary>
    public bool InStock => Available is > 0;
}

/// <summary>Catalog page.</summary>
public sealed record CatalogViewModel(IReadOnlyList<ProductCard> Products, string? Error);

/// <summary>Product page.</summary>
public sealed record ProductDetailsViewModel(Product Product, int? Available);

/// <summary>Login form.</summary>
public sealed class LoginViewModel
{
    /// <summary>Email.</summary>
    [Required(ErrorMessage = "Enter your email address.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    public string Email { get; set; } = string.Empty;

    /// <summary>Password.</summary>
    [Required(ErrorMessage = "Enter your password.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    /// <summary>Where to go after signing in.</summary>
    public string? ReturnUrl { get; set; }
}

/// <summary>Registration form.</summary>
public sealed class RegisterViewModel
{
    /// <summary>Full name.</summary>
    [Required(ErrorMessage = "Enter your name.")]
    [StringLength(120, MinimumLength = 2, ErrorMessage = "Your name must be 2 to 120 characters.")]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    /// <summary>Email.</summary>
    [Required(ErrorMessage = "Enter your email address.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    public string Email { get; set; } = string.Empty;

    /// <summary>Password.</summary>
    [Required(ErrorMessage = "Choose a password.")]
    [StringLength(128, MinimumLength = 8, ErrorMessage = "Use at least 8 characters.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    /// <summary>Password again.</summary>
    [Required(ErrorMessage = "Type the password again.")]
    [Compare(nameof(Password), ErrorMessage = "The passwords do not match.")]
    [DataType(DataType.Password)]
    [Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

/// <summary>Cart and checkout pages.</summary>
public sealed record CartViewModel(IReadOnlyList<CartLine> Lines, decimal Total, string Currency);

/// <summary>My Orders page.</summary>
public sealed record OrdersViewModel(IReadOnlyList<OrderSummary> Orders, string? Error);

/// <summary>Product create/edit form.</summary>
public sealed class ProductFormViewModel
{
    /// <summary>Set when editing.</summary>
    public Guid? ProductId { get; set; }

    /// <summary>Name.</summary>
    [Required(ErrorMessage = "Enter a product name.")]
    [StringLength(120, MinimumLength = 2, ErrorMessage = "The name must be 2 to 120 characters.")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Description.</summary>
    [StringLength(1000, ErrorMessage = "Keep the description under 1000 characters.")]
    public string? Description { get; set; }

    /// <summary>Price.</summary>
    [Range(typeof(decimal), "0.01", "1000000", ErrorMessage = "Enter a price between 0.01 and 1,000,000.")]
    public decimal Price { get; set; }

    /// <summary>Currency.</summary>
    [Required]
    [RegularExpression("^[A-Za-z]{3}$", ErrorMessage = "Use a 3-letter currency code, e.g. PHP.")]
    public string Currency { get; set; } = "PHP";

    /// <summary>Active flag.</summary>
    [Display(Name = "Visible in the store")]
    public bool IsActive { get; set; } = true;
}

/// <summary>Admin products page.</summary>
public sealed record AdminProductsViewModel(IReadOnlyList<Product> Products, ProductFormViewModel NewProduct, string? Error);

/// <summary>One row of the admin stock page.</summary>
public sealed record StockRow(Guid ProductId, string Name, bool IsActive, int OnHand, int Reserved, int Available);

/// <summary>Admin stock page.</summary>
public sealed record AdminStockViewModel(IReadOnlyList<StockRow> Rows, string? Error);

/// <summary>Chaos and saga testing page.</summary>
public sealed record AdminChaosViewModel(
    string? PaymentMode,
    DateTimeOffset? PaymentModeUpdatedAt,
    string? OrderMode,
    DateTimeOffset? OrderModeUpdatedAt,
    double? CatalogFailureRate,
    int? CatalogDelayMs,
    IReadOnlyList<string> Problems);

/// <summary>Friendly error page.</summary>
public sealed record ErrorViewModel(int StatusCode, string Title, string Message, string? CorrelationId);
