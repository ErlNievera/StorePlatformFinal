namespace Store.Contracts.Events;

/// <summary>
/// An event that announces an outcome to whoever is interested, with no consumer required
/// (<see cref="OrderConfirmed"/>, <see cref="OrderCancelled"/>). Publishers do not treat
/// "no queue bound" as an error for these, unlike saga commands and replies, which must
/// always reach their consumer.
/// </summary>
public interface INotificationEvent : IIntegrationEvent;
