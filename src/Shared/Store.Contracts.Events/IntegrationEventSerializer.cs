using System.Text.Json;

namespace Store.Contracts.Events;

/// <summary>
/// JSON encoding shared by every publisher and consumer, so message bodies look the same
/// regardless of which service produced them: camelCase property names, UTF-8 bytes,
/// <c>application/json</c> content type.
/// </summary>
public static class IntegrationEventSerializer
{
    /// <summary>Content type to set on published RabbitMQ messages.</summary>
    public const string ContentType = "application/json";

    /// <summary>Serializer options used for every event body (read-only).</summary>
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    /// <summary>Serializes an event to a UTF-8 JSON message body.</summary>
    /// <typeparam name="TEvent">Event record type.</typeparam>
    /// <param name="integrationEvent">The event to publish.</param>
    public static byte[] Serialize<TEvent>(TEvent integrationEvent) where TEvent : IIntegrationEvent
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        return JsonSerializer.SerializeToUtf8Bytes(integrationEvent, integrationEvent.GetType(), Options);
    }

    /// <summary>Deserializes a message body into a known event type.</summary>
    /// <typeparam name="TEvent">Expected event record type.</typeparam>
    /// <param name="body">UTF-8 JSON message body.</param>
    /// <exception cref="JsonException">The body is not valid JSON for <typeparamref name="TEvent"/>.</exception>
    public static TEvent Deserialize<TEvent>(ReadOnlySpan<byte> body) where TEvent : IIntegrationEvent =>
        JsonSerializer.Deserialize<TEvent>(body, Options)
        ?? throw new JsonException($"Message body is null; expected {typeof(TEvent).Name}.");

    /// <summary>Deserializes a message body using its routing key to pick the event type.</summary>
    /// <param name="routingKey">Routing key the message was published with.</param>
    /// <param name="body">UTF-8 JSON message body.</param>
    /// <exception cref="ArgumentException">The routing key does not belong to a known event.</exception>
    /// <exception cref="JsonException">The body is not valid JSON for the event type.</exception>
    public static IIntegrationEvent Deserialize(string routingKey, ReadOnlySpan<byte> body)
    {
        if (!EventRoutingKeys.TryGetEventType(routingKey, out var eventType))
        {
            throw new ArgumentException($"Unknown routing key '{routingKey}'.", nameof(routingKey));
        }

        return JsonSerializer.Deserialize(body, eventType, Options) as IIntegrationEvent
            ?? throw new JsonException($"Message body is null; expected {eventType.Name}.");
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = false,
        };
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}
