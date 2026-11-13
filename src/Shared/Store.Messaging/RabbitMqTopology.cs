using RabbitMQ.Client;

namespace Store.Messaging;

/// <summary>
/// Declares the broker objects every service relies on. All declarations are idempotent,
/// so each publisher and consumer declares what it needs at startup.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><c>store.events</c> - durable topic exchange all events are published to.</item>
/// <item><c>store.dlx</c> - durable direct exchange used as dead-letter exchange.</item>
/// <item><c>&lt;queue&gt;</c> - durable consumer queue bound to <c>store.events</c>; it dead-letters
/// into <c>store.dlx</c> with its own name as routing key.</item>
/// <item><c>&lt;queue&gt;.dlq</c> - durable dead-letter queue bound to <c>store.dlx</c> with routing key
/// <c>&lt;queue&gt;</c>, so every consumer queue has its own DLQ.</item>
/// </list>
/// </remarks>
public static class RabbitMqTopology
{
    /// <summary>Suffix of every dead-letter queue.</summary>
    public const string DeadLetterQueueSuffix = ".dlq";

    /// <summary>Name of the dead-letter queue that belongs to <paramref name="queueName"/>.</summary>
    public static string DeadLetterQueueName(string queueName) => queueName + DeadLetterQueueSuffix;

    /// <summary>Declares the event exchange and the dead-letter exchange.</summary>
    public static async Task DeclareExchangesAsync(IChannel channel, RabbitMqOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(options);

        await channel.ExchangeDeclareAsync(
            exchange: options.ExchangeName,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);

        await channel.ExchangeDeclareAsync(
            exchange: options.DeadLetterExchangeName,
            type: ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Declares a durable consumer queue, its dead-letter queue, and binds the consumer queue to
    /// <c>store.events</c> for each routing key.
    /// </summary>
    public static async Task DeclareConsumerQueueAsync(
        IChannel channel,
        RabbitMqOptions options,
        string queueName,
        IEnumerable<string> routingKeys,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);
        ArgumentNullException.ThrowIfNull(routingKeys);

        await DeclareExchangesAsync(channel, options, cancellationToken);

        var deadLetterQueue = DeadLetterQueueName(queueName);
        await channel.QueueDeclareAsync(
            queue: deadLetterQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);
        await channel.QueueBindAsync(
            queue: deadLetterQueue,
            exchange: options.DeadLetterExchangeName,
            routingKey: queueName,
            arguments: null,
            cancellationToken: cancellationToken);

        var queueArguments = new Dictionary<string, object?>
        {
            ["x-dead-letter-exchange"] = options.DeadLetterExchangeName,
            ["x-dead-letter-routing-key"] = queueName,
        };
        await channel.QueueDeclareAsync(
            queue: queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: queueArguments,
            cancellationToken: cancellationToken);

        foreach (var routingKey in routingKeys)
        {
            await channel.QueueBindAsync(
                queue: queueName,
                exchange: options.ExchangeName,
                routingKey: routingKey,
                arguments: null,
                cancellationToken: cancellationToken);
        }
    }
}
