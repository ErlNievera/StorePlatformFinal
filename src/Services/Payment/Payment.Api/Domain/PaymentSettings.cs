namespace Payment.Api.Domain;

/// <summary>On-demand failure switch for demonstrating saga compensation.</summary>
public enum PaymentFailureMode
{
    /// <summary>Charges succeed.</summary>
    None,

    /// <summary>Every new charge is declined; refunds are unaffected.</summary>
    DeclineCharge,
}

/// <summary>
/// Single-row settings table (id 1). Kept in the database so the switch survives restarts and
/// takes effect immediately for every consumer, without code edits or stopping the service.
/// </summary>
public sealed class PaymentSettings
{
    /// <summary>Id of the only settings row.</summary>
    public const int SingletonId = 1;

    private PaymentSettings()
    {
    }

    /// <summary>Creates the settings row.</summary>
    public PaymentSettings(PaymentFailureMode failureMode, DateTimeOffset now)
    {
        Id = SingletonId;
        FailureMode = failureMode;
        UpdatedAt = now;
    }

    /// <summary>Always <see cref="SingletonId"/>.</summary>
    public int Id { get; private set; }

    /// <summary>Current failure mode.</summary>
    public PaymentFailureMode FailureMode { get; private set; }

    /// <summary>When the mode last changed (UTC).</summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Changes the failure mode.</summary>
    public void SetFailureMode(PaymentFailureMode mode, DateTimeOffset now)
    {
        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown failure mode.");
        }

        FailureMode = mode;
        UpdatedAt = now;
    }
}
