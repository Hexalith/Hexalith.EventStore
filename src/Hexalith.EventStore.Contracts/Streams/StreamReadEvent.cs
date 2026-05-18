using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.Contracts.Streams;

/// <summary>
/// Public representation of a single event returned by stream replay/read APIs.
/// </summary>
/// <param name="SequenceNumber">The stream sequence number.</param>
/// <param name="EventTypeName">The persisted event type name.</param>
/// <param name="Payload">The serialized event payload bytes. Record equality compares this array by reference; compare contents explicitly when needed.</param>
/// <param name="SerializationFormat">The payload serialization format.</param>
/// <param name="MetadataVersion">The event metadata schema version.</param>
/// <param name="MessageId">The persisted event message identifier.</param>
/// <param name="CorrelationId">Optional correlation identifier.</param>
/// <param name="CausationId">Optional causation identifier.</param>
/// <param name="Timestamp">The event timestamp.</param>
/// <param name="UserId">Optional authenticated subject identifier.</param>
/// <param name="ProtectionMetadata">Optional payload protection metadata recorded by EventStore. <see langword="null"/> indicates legacy compatibility.</param>
public sealed record StreamReadEvent(
    long SequenceNumber,
    string EventTypeName,
    byte[] Payload,
    string SerializationFormat,
    int MetadataVersion,
    string MessageId,
    string? CorrelationId,
    string? CausationId,
    DateTimeOffset Timestamp,
    string? UserId,
    EventStorePayloadProtectionMetadata? ProtectionMetadata = null);
