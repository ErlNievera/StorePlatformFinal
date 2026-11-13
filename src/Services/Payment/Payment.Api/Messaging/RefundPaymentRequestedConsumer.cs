using Microsoft.Extensions.Options;
using Payment.Api.Application;
using Payment.Api.Data;
using Store.Contracts.Events;
using Store.Messaging;

namespace Payment.Api.Messaging;

/// <summary>
/// Compensation: refunds a charged payment and publishes <see cref="PaymentRefunded"/>.
/// Queue <c>payment.refund-requested</c>. A refund for an unknown or declined payment fails,
/// is retried, and ends up in <c>payment.refund-requested.dlq</c> for investigation.
/// </summary>
public sealed class RefundPaymentRequestedConsumer(
    IRabbitMqConnection connection,
    IServiceScopeFactory scopeFactory,
    IOptions<RabbitMqOptions> options,
    ILogger<RefundPaymentRequestedConsumer> logger)
    : EventConsumer<RefundPaymentRequested>(connection, scopeFactory, options, logger)
{
    /// <summary>Queue name.</summary>
    public const string Queue = "payment.refund-requested";

    /// <inheritdoc />
    protected override string QueueName => Queue;

    /// <inheritdoc />
    protected override async Task HandleAsync(RefundPaymentRequested message, IServiceProvider services, CancellationToken cancellationToken)
    {
        var db = services.GetRequiredService<PaymentDbContext>();
        var processor = services.GetRequiredService<PaymentProcessor>();
        var publisher = services.GetRequiredService<IEventPublisher>();

        PaymentRefunded? refunded = null;
        var applied = await db.ExecuteIdempotentAsync(
            message,
            ConsumerName,
            async ct => refunded = await processor.RefundAsync(message, ct),
            cancellationToken);

        if (!applied || refunded is null)
        {
            Logger.LogInformation(
                "RefundPaymentRequested {MessageId} for order {OrderId} was already processed; republishing its outcome",
                message.MessageId, message.OrderId);
            refunded = await processor.DescribeRefundAsync(message, cancellationToken);
        }

        Logger.LogInformation(
            "Refunded {Amount} {Currency} for order {OrderId} (payment {PaymentId}): {Reason}",
            refunded.Amount, refunded.Currency, message.OrderId, refunded.PaymentId, refunded.Reason);
        await publisher.PublishAsync(refunded, cancellationToken);
    }
}
