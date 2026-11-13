using System.ComponentModel.DataAnnotations;

namespace Store.Messaging;

/// <summary>
/// RabbitMQ settings, bound from the <c>RabbitMq</c> configuration section
/// (environment variables <c>RabbitMq__HostName</c>, <c>RabbitMq__UserName</c>, ...).
/// </summary>
public sealed class RabbitMqOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "RabbitMq";

    /// <summary>Broker host name (the compose service name inside Docker).</summary>
    [Required]
    public string HostName { get; set; } = "localhost";

    /// <summary>AMQP port.</summary>
    [Range(1, 65535)]
    public int Port { get; set; } = 5672;

    /// <summary>Broker user.</summary>
    [Required]
    public string UserName { get; set; } = "guest";

    /// <summary>Broker password.</summary>
    [Required]
    public string Password { get; set; } = "guest";

    /// <summary>Virtual host.</summary>
    [Required]
    public string VirtualHost { get; set; } = "/";

    /// <summary>Topic exchange every event is published to.</summary>
    [Required]
    public string ExchangeName { get; set; } = "store.events";

    /// <summary>Dead-letter exchange; each consumer queue dead-letters into <c>&lt;queue&gt;.dlq</c> through it.</summary>
    [Required]
    public string DeadLetterExchangeName { get; set; } = "store.dlx";

    /// <summary>How many times a consumer tries a message before sending it to its dead-letter queue.</summary>
    [Range(1, 20)]
    public int MaxDeliveryAttempts { get; set; } = 3;

    /// <summary>How many times a publish is retried when the broker does not confirm it.</summary>
    [Range(0, 10)]
    public int PublishRetryCount { get; set; } = 3;

    /// <summary>Unacknowledged messages a consumer may hold at once.</summary>
    [Range(1, 1000)]
    public ushort PrefetchCount { get; set; } = 10;

    /// <summary>Name shown for this client's connection in the management UI.</summary>
    public string? ClientProvidedName { get; set; }
}
