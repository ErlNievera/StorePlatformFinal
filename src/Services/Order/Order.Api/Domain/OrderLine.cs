namespace Order.Api.Domain;

/// <summary>One product on an order, with the name and price snapshot taken from Catalog at placement.</summary>
public sealed class OrderLine
{
    /// <summary>Maximum length of <see cref="ProductName"/>.</summary>
    public const int ProductNameMaxLength = 120;

    private OrderLine()
    {
    }

    /// <summary>Creates a line.</summary>
    public OrderLine(Guid productId, string productName, decimal unitPrice, int quantity)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(productId, Guid.Empty);
        ArgumentException.ThrowIfNullOrWhiteSpace(productName);
        ArgumentOutOfRangeException.ThrowIfNegative(unitPrice);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);

        Id = Guid.NewGuid();
        ProductId = productId;
        ProductName = productName.Length <= ProductNameMaxLength ? productName : productName[..ProductNameMaxLength];
        UnitPrice = decimal.Round(unitPrice, 2, MidpointRounding.AwayFromZero);
        Quantity = quantity;
    }

    /// <summary>Primary key.</summary>
    public Guid Id { get; private set; }

    /// <summary>Owning order.</summary>
    public Guid OrderId { get; private set; }

    /// <summary>Catalog product id.</summary>
    public Guid ProductId { get; private set; }

    /// <summary>Name snapshot.</summary>
    public string ProductName { get; private set; } = string.Empty;

    /// <summary>Price snapshot.</summary>
    public decimal UnitPrice { get; private set; }

    /// <summary>Units ordered.</summary>
    public int Quantity { get; private set; }

    /// <summary>UnitPrice x Quantity.</summary>
    public decimal LineTotal => UnitPrice * Quantity;
}
