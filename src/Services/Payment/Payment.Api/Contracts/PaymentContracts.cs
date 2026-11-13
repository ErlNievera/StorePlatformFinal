using System.ComponentModel.DataAnnotations;
using Payment.Api.Domain;

namespace Payment.Api.Contracts;

/// <summary>Schema <c>Payment</c> in payment.v1.yaml.</summary>
public sealed record PaymentResponse(
    Guid PaymentId,
    Guid OrderId,
    Guid CustomerId,
    decimal Amount,
    string Currency,
    PaymentStatus Status,
    string? FailureReason,
    string? RefundReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    /// <summary>Maps an entity.</summary>
    public static PaymentResponse From(OrderPayment payment) => new(
        payment.PaymentId,
        payment.OrderId,
        payment.CustomerId,
        payment.Amount,
        payment.Currency,
        payment.Status,
        payment.FailureReason,
        payment.RefundReason,
        payment.CreatedAt,
        payment.UpdatedAt);
}

/// <summary>Schema <c>PaymentFailureModeRequest</c>.</summary>
public sealed class PaymentFailureModeRequest
{
    /// <summary>New mode.</summary>
    [Required]
    public PaymentFailureMode? Mode { get; init; }
}

/// <summary>Schema <c>PaymentFailureModeResponse</c>.</summary>
public sealed record PaymentFailureModeResponse(PaymentFailureMode Mode, DateTimeOffset UpdatedAt)
{
    /// <summary>Maps the settings row.</summary>
    public static PaymentFailureModeResponse From(PaymentSettings settings) => new(settings.FailureMode, settings.UpdatedAt);
}
