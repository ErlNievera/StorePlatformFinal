namespace Store.Messaging;

/// <summary>
/// A message a consumer has already handled. Stored in the consuming service's own database
/// (table <c>ProcessedMessages</c>) in the same transaction as the business change, so a
/// redelivered message is recognised and skipped.
/// </summary>
public sealed class ProcessedMessage
{
    /// <summary>Maximum length of <see cref="Consumer"/>.</summary>
    public const int ConsumerMaxLength = 200;

    /// <summary><c>MessageId</c> of the handled event.</summary>
    public Guid MessageId { get; set; }

    /// <summary>Consumer (queue) that handled it; part of the key so two consumers of the same event stay independent.</summary>
    public string Consumer { get; set; } = string.Empty;

    /// <summary>Event type name, for troubleshooting.</summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>When the message was handled (UTC).</summary>
    public DateTimeOffset ProcessedAt { get; set; }
}
