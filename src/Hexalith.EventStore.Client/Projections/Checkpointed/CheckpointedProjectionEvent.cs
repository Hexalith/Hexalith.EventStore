using Hexalith.EventStore.Client.Subscriptions;
using Hexalith.EventStore.Contracts.Streams;

namespace Hexalith.EventStore.Client.Projections.Checkpointed;

/// <summary>
/// One event offered to a checkpointed projection, either as a live delivery or as authoritative history.
/// </summary>
/// <param name="Stream">The authoritative stream the event belongs to.</param>
/// <param name="Sequence">The aggregate sequence of the event.</param>
/// <param name="Evidence">The event identity (message, type, payload fingerprint).</param>
/// <param name="Timestamp">When EventStore persisted the event.</param>
/// <param name="CorrelationId">The persisted correlation identifier, when present.</param>
public sealed record CheckpointedProjectionEvent(
    CheckpointedProjectionStream Stream,
    long Sequence,
    CheckpointedProjectionEvidence Evidence,
    DateTimeOffset Timestamp,
    string? CorrelationId) {
    /// <summary>
    /// Gets the already-decoded event payload supplied by a live delivery, when available.
    /// </summary>
    /// <remarks>Authoritative history events carry <see cref="Payload"/> instead and leave this <see langword="null"/>.</remarks>
    public object? DecodedEvent { get; init; }

    /// <summary>Gets the exact persisted payload bytes supplied by authoritative history, when available.</summary>
    public byte[]? Payload { get; init; }

    /// <summary>Gets the persisted payload serialization format, when known.</summary>
    public string? SerializationFormat { get; init; }

    /// <summary>Gets the persisted payload version, when known; <see langword="null"/> denotes version one.</summary>
    public int? PayloadVersion { get; init; }

    /// <summary>
    /// Creates a live-delivery event from the processor context and its decoded payload.
    /// </summary>
    /// <param name="decodedEvent">The decoded event payload dispatched to the handler.</param>
    /// <param name="context">The processor context carrying envelope metadata.</param>
    /// <returns>The checkpointed projection event.</returns>
    /// <exception cref="ArgumentException">The context lacks the domain, event type, or payload fingerprint.</exception>
    public static CheckpointedProjectionEvent FromDelivery(object decodedEvent, EventStoreDomainEventContext context) {
        ArgumentNullException.ThrowIfNull(decodedEvent);
        ArgumentNullException.ThrowIfNull(context);
        if (string.IsNullOrWhiteSpace(context.Domain)
            || string.IsNullOrWhiteSpace(context.EventType)
            || string.IsNullOrWhiteSpace(context.PayloadFingerprint)) {
            throw new ArgumentException(
                "A checkpointed delivery requires the envelope domain, event type, and payload fingerprint.",
                nameof(context));
        }

        return new CheckpointedProjectionEvent(
            new CheckpointedProjectionStream(context.TenantId, context.Domain, context.AggregateId),
            context.SequenceNumber,
            new CheckpointedProjectionEvidence(context.MessageId, context.EventType, context.PayloadFingerprint),
            context.Timestamp,
            context.CorrelationId) {
            DecodedEvent = decodedEvent,
        };
    }

    /// <summary>
    /// Creates an authoritative history event from a stream read result.
    /// </summary>
    /// <param name="stream">The stream that was read.</param>
    /// <param name="streamEvent">The persisted event returned by the range read.</param>
    /// <returns>The checkpointed projection event.</returns>
    public static CheckpointedProjectionEvent FromHistory(CheckpointedProjectionStream stream, StreamReadEvent streamEvent) {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(streamEvent);
        byte[] payload = streamEvent.Payload ?? [];
        return new CheckpointedProjectionEvent(
            stream,
            streamEvent.SequenceNumber,
            new CheckpointedProjectionEvidence(
                streamEvent.MessageId,
                streamEvent.EventTypeName,
                EventStorePayloadFingerprint.Compute(payload)),
            streamEvent.Timestamp,
            streamEvent.CorrelationId) {
            Payload = payload,
            SerializationFormat = streamEvent.SerializationFormat,
            PayloadVersion = streamEvent.PayloadVersion,
        };
    }
}
