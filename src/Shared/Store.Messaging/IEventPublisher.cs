using Store.Contracts.Events;

namespace Store.Messaging;

/// <summary>Publishes integration events to the <c>store.events</c> exchange.</summary>
public interface IEventPublisher
{
    /// <summary>
    /// Publishes <paramref name="integrationEvent"/> with its routing key and waits for the
    /// broker's publisher confirm. Unconfirmed publishes, and unroutable ones (no queue bound) for
    /// every event except an <see cref="INotificationEvent"/>, are retried
    /// <see cref="RabbitMqOptions.PublishRetryCount"/> times before the exception is rethrown.
    /// </summary>
    Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken = default)
        where TEvent : IIntegrationEvent;
}
