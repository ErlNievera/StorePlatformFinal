namespace Inventory.Api.Domain;

/// <summary>Stock of one Catalog product.</summary>
/// <remarks>
/// Invariants: <c>0 &lt;= QuantityReserved &lt;= QuantityOnHand</c>. Reserved units stay in
/// <see cref="QuantityOnHand"/> until the order is cancelled (released) - this capstone does not
/// model shipping, so confirmed orders keep their units reserved.
/// </remarks>
public sealed class StockItem
{
    private StockItem()
    {
    }

    /// <summary>Creates a stock record for a product.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="quantityOnHand"/> is negative.</exception>
    public StockItem(Guid productId, int quantityOnHand, DateTimeOffset now)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(productId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfNegative(quantityOnHand);

        ProductId = productId;
        QuantityOnHand = quantityOnHand;
        QuantityReserved = 0;
        UpdatedAt = now;
    }

    /// <summary>Catalog product id (primary key).</summary>
    public Guid ProductId { get; private set; }

    /// <summary>Physical units in stock, including reserved units.</summary>
    public int QuantityOnHand { get; private set; }

    /// <summary>Units held by active reservations.</summary>
    public int QuantityReserved { get; private set; }

    /// <summary>Units new orders can reserve.</summary>
    public int QuantityAvailable => QuantityOnHand - QuantityReserved;

    /// <summary>Last change (UTC).</summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Optimistic concurrency token (PostgreSQL <c>xmin</c>).</summary>
    public uint Version { get; private set; }

    /// <summary>Adds or removes units on hand (admin stock adjustment).</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="delta"/> is zero.</exception>
    /// <exception cref="StockConflictException">On-hand would become negative or less than reserved.</exception>
    public void Adjust(int delta, DateTimeOffset now)
    {
        ArgumentOutOfRangeException.ThrowIfZero(delta);

        var newOnHand = (long)QuantityOnHand + delta;
        if (newOnHand < 0)
        {
            throw new StockConflictException(
                $"Cannot remove {-delta} unit(s): only {QuantityOnHand} on hand.");
        }

        if (newOnHand < QuantityReserved)
        {
            throw new StockConflictException(
                $"Cannot reduce stock to {newOnHand}: {QuantityReserved} unit(s) are reserved for orders.");
        }

        if (newOnHand > int.MaxValue)
        {
            throw new StockConflictException("Stock on hand would exceed the maximum supported quantity.");
        }

        QuantityOnHand = (int)newOnHand;
        UpdatedAt = now;
    }

    /// <summary>Whether <paramref name="quantity"/> units can be reserved now.</summary>
    public bool CanReserve(int quantity) => quantity > 0 && QuantityAvailable >= quantity;

    /// <summary>Holds units for an order.</summary>
    /// <exception cref="StockConflictException">Fewer than <paramref name="quantity"/> units are available.</exception>
    public void Reserve(int quantity, DateTimeOffset now)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        if (!CanReserve(quantity))
        {
            throw new StockConflictException(
                $"Cannot reserve {quantity} unit(s) of {ProductId}: only {QuantityAvailable} available.");
        }

        QuantityReserved += quantity;
        UpdatedAt = now;
    }

    /// <summary>Returns reserved units to available stock.</summary>
    /// <exception cref="StockConflictException">More units would be released than are reserved.</exception>
    public void Release(int quantity, DateTimeOffset now)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        if (quantity > QuantityReserved)
        {
            throw new StockConflictException(
                $"Cannot release {quantity} unit(s) of {ProductId}: only {QuantityReserved} reserved.");
        }

        QuantityReserved -= quantity;
        UpdatedAt = now;
    }
}
