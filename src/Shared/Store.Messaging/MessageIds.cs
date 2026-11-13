using System.Security.Cryptography;
using System.Text;

namespace Store.Messaging;

/// <summary>Helpers for event message ids.</summary>
public static class MessageIds
{
    /// <summary>
    /// Derives a stable message id for an event published in response to another message.
    /// </summary>
    /// <remarks>
    /// A consumer commits its business change, then publishes the outcome. If the publish fails
    /// after the commit, the incoming message is redelivered, recognised as processed, and the
    /// outcome is published again. Deriving the outcome's id from the incoming id means both
    /// publishes carry the same <c>MessageId</c>, so the next consumer's <c>ProcessedMessages</c>
    /// check treats the second one as a duplicate.
    /// </remarks>
    /// <param name="sourceMessageId">Id of the message being handled.</param>
    /// <param name="purpose">What is being published, e.g. the outcome event type name.</param>
    public static Guid DeriveFrom(Guid sourceMessageId, string purpose)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);

        var purposeBytes = Encoding.UTF8.GetBytes(purpose);
        var input = new byte[16 + purposeBytes.Length];
        sourceMessageId.TryWriteBytes(input);
        purposeBytes.CopyTo(input, 16);

        Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(input, hash);

        var guidBytes = hash[..16].ToArray();
        guidBytes[7] = (byte)((guidBytes[7] & 0x0F) | 0x50); // name-based version nibble
        guidBytes[8] = (byte)((guidBytes[8] & 0x3F) | 0x80); // RFC 4122 variant
        return new Guid(guidBytes);
    }
}
