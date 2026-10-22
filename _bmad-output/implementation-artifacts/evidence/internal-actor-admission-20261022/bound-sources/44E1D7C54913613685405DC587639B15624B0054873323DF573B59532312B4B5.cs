using Hexalith.EventStore.Client.Events;

namespace Hexalith.EventStore.Server.Events;

/// <summary>Owns one effective logical view and retains the event's addressed identity.</summary>
internal sealed class DaprLogicalEventView : IDisposable
{
    private readonly ResolvedLogicalEvent _resolved;
    private EventBufferReservation? _metadataReservation;

    /// <summary>Captures immutable source metadata and takes ownership of the resolved payload.</summary>
    internal DaprLogicalEventView(EventEnvelope source, ResolvedLogicalEvent resolved, int readablePayloadLength)
        : this(source, resolved, readablePayloadLength, metadataReservation: null)
    {
    }

    /// <summary>Takes ownership of the resolved payload and its retained metadata reservation.</summary>
    internal DaprLogicalEventView(EventEnvelope source, ResolvedLogicalEvent resolved, int readablePayloadLength,
        EventBufferReservation? metadataReservation)
        : this(source, resolved, readablePayloadLength, metadataReservation, source.ApplicationPayloadDigest, source.SerializationFormat)
    {
    }

    /// <summary>Takes ownership of an addressed source's recomputed application digest and exact readable format.</summary>
    internal DaprLogicalEventView(EventEnvelope source, ResolvedLogicalEvent resolved, int readablePayloadLength,
        EventBufferReservation? metadataReservation, string? applicationLogicalDigest, string readableFormat)
    {
        Source = source;
        MessageId = source.MessageId;
        CorrelationId = source.CorrelationId;
        CausationId = source.CausationId;
        SequenceNumber = source.SequenceNumber;
        StoredPayloadLength = source.Payload.Length;
        ReadablePayloadLength = readablePayloadLength;
        _resolved = resolved;
        _metadataReservation = metadataReservation;
        ApplicationLogicalDigest = applicationLogicalDigest;
        ReadableFormat = readableFormat;
    }

    /// <summary>Gets the addressed stored envelope. Its payload bytes stay the actor value.</summary>
    internal EventEnvelope Source { get; }

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

    /// <summary>Gets recomputed application evidence separately from every historical StoredDigest meaning.</summary>
    internal string? ApplicationLogicalDigest { get; }

    /// <summary>Gets the post-unprotection application format before any upcast.</summary>
    internal string ReadableFormat { get; }

    /// <summary>Transfers the retained source metadata charge to a longer-lived range owner exactly once.</summary>
    internal EventBufferReservation? TakeMetadataReservation()
        => Interlocked.Exchange(ref _metadataReservation, null);

    /// <inheritdoc/>
    public void Dispose()
    {
        _resolved.Dispose();
        Interlocked.Exchange(ref _metadataReservation, null)?.Dispose();
    }
}
