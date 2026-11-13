namespace Order.Api.Domain;

/// <summary>Order lifecycle driven by the saga.</summary>
public enum OrderStatus
{
    /// <summary>Placed; waiting for Inventory to reserve stock.</summary>
    Pending,

    /// <summary>Stock reserved; waiting for Payment.</summary>
    StockReserved,

    /// <summary>Payment charged; about to be confirmed.</summary>
    PaymentCharged,

    /// <summary>Saga completed successfully.</summary>
    Confirmed,

    /// <summary>A step failed and compensation (release, refund) is in progress.</summary>
    Cancelling,

    /// <summary>Saga ended unsuccessfully; every compensation has completed.</summary>
    Cancelled,
}
