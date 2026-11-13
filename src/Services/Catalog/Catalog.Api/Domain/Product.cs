namespace Catalog.Api.Domain;

/// <summary>A product that can be sold. Catalog is the source of truth for name, price and active state.</summary>
public sealed class Product
{
    /// <summary>Maximum length of <see cref="Name"/>.</summary>
    public const int NameMaxLength = 120;

    /// <summary>Maximum length of <see cref="Description"/>.</summary>
    public const int DescriptionMaxLength = 1000;

    /// <summary>Default currency for the demo store.</summary>
    public const string DefaultCurrency = "PHP";

    private Product()
    {
    }

    /// <summary>Creates a product with a new id.</summary>
    public Product(string name, string description, decimal price, string currency, bool isActive, DateTimeOffset now)
        : this(Guid.NewGuid(), name, description, price, currency, isActive, now)
    {
    }

    /// <summary>Creates a product with a known id (seed data).</summary>
    public Product(Guid productId, string name, string description, decimal price, string currency, bool isActive, DateTimeOffset now)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(productId, Guid.Empty);
        ProductId = productId;
        CreatedAt = now;
        Apply(name, description, price, currency, isActive, now);
    }

    /// <summary>Primary key.</summary>
    public Guid ProductId { get; private set; }

    /// <summary>Display name (unique).</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Description shown on the product page.</summary>
    public string Description { get; private set; } = string.Empty;

    /// <summary>Current unit price.</summary>
    public decimal Price { get; private set; }

    /// <summary>ISO 4217 currency code.</summary>
    public string Currency { get; private set; } = DefaultCurrency;

    /// <summary>Only active products can be browsed publicly and ordered.</summary>
    public bool IsActive { get; private set; }

    /// <summary>When the product was created (UTC).</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Last change (UTC).</summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Optimistic concurrency token (PostgreSQL <c>xmin</c>).</summary>
    public uint Version { get; private set; }

    /// <summary>Replaces every editable field.</summary>
    public void Update(string name, string description, decimal price, string currency, bool isActive, DateTimeOffset now) =>
        Apply(name, description, price, currency, isActive, now);

    private void Apply(string name, string description, decimal price, string currency, bool isActive, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(description);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(price);
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);
        if (currency.Trim().Length != 3)
        {
            throw new ArgumentException("Currency must be a 3-letter ISO 4217 code.", nameof(currency));
        }

        Name = name.Trim();
        Description = description.Trim();
        Price = decimal.Round(price, 2, MidpointRounding.AwayFromZero);
        Currency = currency.Trim().ToUpperInvariant();
        IsActive = isActive;
        UpdatedAt = now;
    }
}
