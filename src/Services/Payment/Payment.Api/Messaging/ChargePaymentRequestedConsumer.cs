using Microsoft.Extensions.Options;
using Payment.Api.Application;
using Payment.Api.Data;
using Store.Contracts.Events;
using Store.Messaging;

namespace Payment.Api.Messaging;

/// <summary>
/// Saga step 2: charges the order and publishes <see cref="PaymentCharged"/>, or
/// <see cref="PaymentFailed"/> while the failure mode is <c>DeclineCharge</c>.
/// Queue <c>payment.charge-requested</c>.
/// </summary>
public sealed class ChargePaymentRequestedConsumer(
    IRabbitMqConnection connection,
    IServiceScopeFactory scopeFactory,
    IOptions<RabbitMqOptions> options,
    ILogger<ChargePaymentRequestedConsumer> logger)
    : EventConsumer<ChargePaymentRequested>(connection, scopeFactory, options, logger)
{
    /// <summary>Queue name.</summary>
    public const string Queue = "payment.charge-requested";

    /// <inheritdoc />
    protected override string QueueName => Queue;

    /// <inheritdoc />
    protected override async Task HandleAsync(ChargePaymentRequested message, IServiceProvider services, CancellationToken cancellationToken)
    {
        var db = services.GetRequiredService<PaymentDbContext>();
        var processor = services.GetRequiredService<PaymentProcessor>();
        var publisher = services.GetRequiredService<IEventPublisher>();

        IIntegrationEvent? outcome = null;
        var applied = await db.ExecuteIdempotentAsync(
            message,
            ConsumerName,
            async ct => outcome = await processor.ChargeAsync(message, ct),
            cancellationToken);

        if (!applied || outcome is null)
        {
            Logger.LogInformation(
                "ChargePaymentRequested {MessageId} for order {OrderId} was already processed; republishing its outcome",
                message.MessageId, message.OrderId);
            outcome = await processor.DescribeChargeAsync(message, cancellationToken);
        }

        switch (outcome)
        {
            case PaymentCharged charged:
                Logger.LogInformation(
                    "Charged {Amount} {Currency} for order {OrderId} (payment {PaymentId})",
                    charged.Amount, charged.Currency, message.OrderId, charged.PaymentId);
                await publisher.PublishAsync(charged, cancellationToken);
                break;
            case PaymentFailed failed:
                Logger.LogWarning(
                    "Declined payment for order {OrderId} (payment {PaymentId}): {Reason}",
                    message.OrderId, failed.PaymentId, failed.Reason);
                await publisher.PublishAsync(failed, cancellationToken);
                break;
            default:
                throw new InvalidOperationException($"Unexpected charge outcome for order {message.OrderId}.");
        }
    }
}
