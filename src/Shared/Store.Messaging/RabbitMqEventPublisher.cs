using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using Store.Contracts.Events;

namespace Store.Messaging;

/// <summary>
/// <see cref="IEventPublisher"/> backed by one confirm-mode channel. With publisher confirmation
/// tracking enabled, <c>BasicPublishAsync</c> completes only after the broker acks the message and
/// throws when it is nacked or, for anything but an <see cref="INotificationEvent"/>, returned as
/// unroutable (<c>mandatory: true</c>).
/// </summary>
internal sealed class RabbitMqEventPublisher(
    IRabbitMqConnection connection,
    IOptions<RabbitMqOptions> options,
    ILogger<RabbitMqEventPublisher> logger) : IEventPublisher, IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IChannel? _channel;

    public async Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken = default)
        where TEvent : IIntegrationEvent
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var settings = options.Value;
        var eventType = integrationEvent.GetType();
        var routingKey = EventRoutingKeys.For(eventType);
        var body = IntegrationEventSerializer.Serialize(integrationEvent);
        var properties = new BasicProperties
        {
            MessageId = integrationEvent.MessageId.ToString(),
            CorrelationId = integrationEvent.CorrelationId,
            ContentType = IntegrationEventSerializer.ContentType,
            DeliveryMode = DeliveryModes.Persistent,
            Type = eventType.Name,
            Timestamp = new AmqpTimestamp(integrationEvent.OccurredAt.ToUnixTimeSeconds()),
        };

        for (var attempt = 1; ; attempt++)
        {
            await _gate.WaitAsync(cancellationToken);
            try
            {
                var channel = await GetChannelAsync(settings, cancellationToken);
                await channel.BasicPublishAsync(
                    exchange: settings.ExchangeName,
                    routingKey: routingKey,
                    mandatory: integrationEvent is not INotificationEvent,
                    basicProperties: properties,
                    body: body,
                    cancellationToken: cancellationToken);

                logger.LogInformation(
                    "Published {EventType} {MessageId} for order {OrderId} to {Exchange} with routing key {RoutingKey} (confirmed)",
                    eventType.Name, integrationEvent.MessageId, integrationEvent.OrderId, settings.ExchangeName, routingKey);
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException && attempt <= settings.PublishRetryCount)
            {
                logger.LogWarning(
                    ex,
                    "Publish of {EventType} {MessageId} was not confirmed (attempt {Attempt} of {MaxAttempts}); retrying",
                    eventType.Name, integrationEvent.MessageId, attempt, settings.PublishRetryCount + 1);
                await DiscardClosedChannelAsync();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(
                    ex,
                    "Publish of {EventType} {MessageId} for order {OrderId} failed after {Attempts} attempts",
                    eventType.Name, integrationEvent.MessageId, integrationEvent.OrderId, attempt);
                await DiscardClosedChannelAsync();
                throw;
            }
            finally
            {
                _gate.Release();
            }

            await Task.Delay(TimeSpan.FromMilliseconds(200 * Math.Pow(2, attempt - 1)), cancellationToken);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null)
        {
            try
            {
                if (_channel.IsOpen)
                {
                    await _channel.CloseAsync();
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error while closing the publisher channel");
            }

            await _channel.DisposeAsync();
            _channel = null;
        }

        _gate.Dispose();
    }

    private async Task<IChannel> GetChannelAsync(RabbitMqOptions settings, CancellationToken cancellationToken)
    {
        if (_channel is { IsOpen: true })
        {
            return _channel;
        }

        await DiscardClosedChannelAsync();

        var amqpConnection = await connection.GetConnectionAsync(cancellationToken);
        var channel = await amqpConnection.CreateChannelAsync(
            new CreateChannelOptions(
                publisherConfirmationsEnabled: true,
                publisherConfirmationTrackingEnabled: true),
            cancellationToken);
        await RabbitMqTopology.DeclareExchangesAsync(channel, settings, cancellationToken);

        _channel = channel;
        return channel;
    }

    private async Task DiscardClosedChannelAsync()
    {
        if (_channel is null || _channel.IsOpen)
        {
            return;
        }

        await _channel.DisposeAsync();
        _channel = null;
    }
}
