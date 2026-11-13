namespace Order.Api.Domain;

/// <summary>On-demand failure switch for demonstrating compensation after payment.</summary>
public enum OrderFailureMode
{
    /// <summary>Orders confirm normally.</summary>
    None,

    /// <summary>Every order fails right after payment is charged: refund, release stock, cancel.</summary>
    FailAfterPayment,
}

/// <summary>Single-row settings table (id 1) holding the failure mode, so it persists and applies immediately.</summary>
public sealed class OrderSettings
{
    /// <summary>Id of the only settings row.</summary>
    public const int SingletonId = 1;

    private OrderSettings()
    {
    }

    /// <summary>Creates the settings row.</summary>
    public OrderSettings(OrderFailureMode failureMode, DateTimeOffset now)
    {
        Id = SingletonId;
        FailureMode = failureMode;
        UpdatedAt = now;
    }

    /// <summary>Always <see cref="SingletonId"/>.</summary>
    public int Id { get; private set; }

    /// <summary>Current failure mode.</summary>
    public OrderFailureMode FailureMode { get; private set; }

    /// <summary>When the mode last changed (UTC).</summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Changes the failure mode.</summary>
    public void SetFailureMode(OrderFailureMode mode, DateTimeOffset now)
    {
        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown failure mode.");
        }

        FailureMode = mode;
        UpdatedAt = now;
    }
}
