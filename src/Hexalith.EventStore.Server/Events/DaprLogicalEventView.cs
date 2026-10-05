using Hexalith.EventStore.Client.Events;

namespace Hexalith.EventStore.Server.Events;

/// <summary>Owns one effective logical view and retains the event's addressed identity.</summary>
internal sealed class DaprLogicalEventView : IDisposable
{
    private readonly ResolvedLogicalEvent _resolved;

    /// <summary>Captures immutable source metadata and takes ownership of the resolved payload.</summary>
    internal DaprLogicalEventView(EventEnvelope source, ResolvedLogicalEvent resolved, int readablePayloadLength)
    {
        MessageId = source.MessageId;
        CorrelationId = source.CorrelationId;
        CausationId = source.CausationId;
        SequenceNumber = source.SequenceNumber;
        StoredPayloadLength = source.Payload.Length;
        ReadablePayloadLength = readablePayloadLength;
        _resolved = resolved;
    }

    /// <summary>Gets the unchanged stored message identifier.</summary>
    internal string MessageId { get; }

    /// <summary>Gets the unchanged correlation identifier.</summary>
    internal string CorrelationId { get; }

    /// <summary>Gets the unchanged causation identifier.</summary>
    internal string CausationId { get; }

    /// <summary>Gets the unchanged aggregate-local sequence.</summary>
    internal long SequenceNumber { get; }

    /// <summary>Gets the measured logical stored-payload byte count for page admission.</summary>
    internal int StoredPayloadLength { get; }

    /// <summary>Gets the original post-unprotection byte count independently of the effective upcast output.</summary>
    internal int ReadablePayloadLength { get; }

    /// <summary>Gets the privately owned current event view.</summary>
    internal ResolvedLogicalEvent Resolved => _resolved;

    /// <inheritdoc/>
    public void Dispose() => _resolved.Dispose();
}
