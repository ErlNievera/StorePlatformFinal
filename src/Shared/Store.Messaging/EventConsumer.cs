using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Store.Contracts.Events;

namespace Store.Messaging;

/// <summary>
/// Base class for a background consumer of one event type from its own durable queue.
/// </summary>
/// <remarks>
/// <para>At startup the consumer declares its queue, its <c>.dlq</c> and the binding for the
/// event's routing key, then consumes with <c>autoAck: false</c>.</para>
/// <para>Each message is handled in a fresh DI scope (so a scoped DbContext is per message).
/// On success it is acked. When the handler throws, the message is nacked with requeue and
/// retried with a short backoff; after <see cref="RabbitMqOptions.MaxDeliveryAttempts"/> failed
/// attempts it is nacked with <c>requeue: false</c>, which moves it to <c>&lt;queue&gt;.dlq</c>
/// through <c>store.dlx</c>. A body that cannot be deserialized goes to the DLQ immediately.
/// Messages are never dropped silently.</para>
/// <para>Handlers must be idempotent: RabbitMQ delivers at least once. Use
/// <see cref="ProcessedMessageExtensions.ExecuteIdempotentAsync{TContext}"/> to record the
/// message id in the same transaction as the business change.</para>
/// </remarks>
/// <typeparam name="TEvent">Event record consumed from the queue.</typeparam>
public abstract class EventConsumer<TEvent> : BackgroundService
    where TEvent : class, IIntegrationEvent
{
    private readonly IRabbitMqConnection _connection;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RabbitMqOptions _options;
    private readonly ConcurrentDictionary<Guid, int> _failedAttempts = new();
    private IChannel? _channel;

    /// <summary>Creates the consumer.</summary>
    protected EventConsumer(
        IRabbitMqConnection connection,
        IServiceScopeFactory scopeFactory,
        IOptions<RabbitMqOptions> options,
        ILogger logger)
    {
        _connection = connection;
        _scopeFactory = scopeFactory;
        _options = options.Value;
        Logger = logger;
    }

    /// <summary>Durable queue name, conventionally <c>&lt;service&gt;.&lt;event&gt;</c>, e.g. <c>inventory.order-placed</c>.</summary>
    protected abstract string QueueName { get; }

    /// <summary>Name recorded in <c>ProcessedMessages</c> for idempotency. Defaults to <see cref="QueueName"/>.</summary>
    protected virtual string ConsumerName => QueueName;

    /// <summary>Logger of the concrete consumer.</summary>
    protected ILogger Logger { get; }

    /// <summary>
    /// Handles one message. Throw to signal failure (the message is retried, then dead-lettered).
    /// </summary>
    /// <param name="message">The deserialized event.</param>
    /// <param name="services">Service provider of the per-message scope.</param>
    /// <param name="cancellationToken">Cancelled when the host is stopping.</param>
    protected abstract Task HandleAsync(TEvent message, IServiceProvider services, CancellationToken cancellationToken);

    /// <summary>Delay before a failed message is requeued; grows with each attempt.</summary>
    protected virtual TimeSpan RetryDelay(int failedAttempts) =>
        TimeSpan.FromMilliseconds(Math.Min(5000, 250 * Math.Pow(2, failedAttempts - 1)));

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var routingKey = EventRoutingKeys.For<TEvent>();
        var connection = await _connection.GetConnectionAsync(stoppingToken);
        var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
        _channel = channel;

        await RabbitMqTopology.DeclareConsumerQueueAsync(channel, _options, QueueName, [routingKey], stoppingToken);
        await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: _options.PrefetchCount, global: false, stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += (_, delivery) => OnReceivedAsync(channel, delivery, stoppingToken);
        await channel.BasicConsumeAsync(queue: QueueName, autoAck: false, consumer: consumer, cancellationToken: stoppingToken);

        Logger.LogInformation(
            "Consuming {EventType} from queue {Queue} (routing key {RoutingKey}, dead-letter queue {DeadLetterQueue})",
            typeof(TEvent).Name, QueueName, routingKey, RabbitMqTopology.DeadLetterQueueName(QueueName));

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            Logger.LogInformation("Stopping consumer for queue {Queue}", QueueName);
        }
    }

    /// <inheritdoc />
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);

        if (_channel is not null)
        {
            if (_channel.IsOpen)
            {
                await _channel.CloseAsync(cancellationToken);
            }

            await _channel.DisposeAsync();
            _channel = null;
        }
    }

    private async Task OnReceivedAsync(IChannel channel, BasicDeliverEventArgs delivery, CancellationToken stoppingToken)
    {
        TEvent message;
        try
        {
            message = IntegrationEventSerializer.Deserialize<TEvent>(delivery.Body.Span);
        }
        catch (JsonException ex)
        {
            Logger.LogError(
                ex,
                "Message {AmqpMessageId} on {Queue} is not a valid {EventType}; sending it to {DeadLetterQueue}",
                delivery.BasicProperties.MessageId, QueueName, typeof(TEvent).Name, RabbitMqTopology.DeadLetterQueueName(QueueName));
            await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: false, CancellationToken.None);
            return;
        }

        try
        {
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                await HandleAsync(message, scope.ServiceProvider, stoppingToken);
            }

            await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, CancellationToken.None);
            _failedAttempts.TryRemove(message.MessageId, out _);
            Logger.LogInformation(
                "Handled {EventType} {MessageId} for order {OrderId} from {Queue} (acked)",
                typeof(TEvent).Name, message.MessageId, message.OrderId, QueueName);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            Logger.LogInformation(
                "Host is stopping; returning {EventType} {MessageId} to {Queue}",
                typeof(TEvent).Name, message.MessageId, QueueName);
            await TryNackAsync(channel, delivery.DeliveryTag, requeue: true);
        }
        catch (Exception ex)
        {
            var attempts = _failedAttempts.AddOrUpdate(message.MessageId, 1, (_, previous) => previous + 1);
            if (attempts >= _options.MaxDeliveryAttempts)
            {
                _failedAttempts.TryRemove(message.MessageId, out _);
                Logger.LogError(
                    ex,
                    "{EventType} {MessageId} for order {OrderId} failed {Attempts} time(s); sending it to {DeadLetterQueue}",
                    typeof(TEvent).Name, message.MessageId, message.OrderId, attempts, RabbitMqTopology.DeadLetterQueueName(QueueName));
                await TryNackAsync(channel, delivery.DeliveryTag, requeue: false);
                return;
            }

            var delay = RetryDelay(attempts);
            Logger.LogWarning(
                ex,
                "{EventType} {MessageId} for order {OrderId} failed (attempt {Attempt} of {MaxAttempts}); requeueing in {DelayMs} ms",
                typeof(TEvent).Name, message.MessageId, message.OrderId, attempts, _options.MaxDeliveryAttempts, delay.TotalMilliseconds);
            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Shutting down: requeue right away so another instance or the next start picks it up.
            }

            await TryNackAsync(channel, delivery.DeliveryTag, requeue: true);
        }
    }

    private async Task TryNackAsync(IChannel channel, ulong deliveryTag, bool requeue)
    {
        try
        {
            await channel.BasicNackAsync(deliveryTag, multiple: false, requeue: requeue, CancellationToken.None);
        }
        catch (Exception ex) when (!channel.IsOpen)
        {
            // The channel closed underneath us; the broker redelivers every unacked message.
            Logger.LogWarning(ex, "Could not nack delivery {DeliveryTag} on {Queue}; the channel is closed and the broker will redeliver it", deliveryTag, QueueName);
        }
    }
}
