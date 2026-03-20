
namespace Hexalith.EventStore.Contracts.Projections;

/// <summary>
/// Wire-format event DTO sent to domain services for projection building.
/// Deliberately excludes Server-internal fields (CausationId, UserId, DomainServiceVersion,
/// GlobalPosition, MetadataVersion, Extensions, MessageId, AggregateId/Type/TenantId/Domain)
/// to maintain the security boundary.
/// </summary>
/// <param name="EventTypeName">The fully qualified event type name for deserialization.</param>
/// <param name="Payload">The serialized event data.</param>
/// <param name="SerializationFormat">The serialization format (e.g., "json").</param>
/// <param name="SequenceNumber">The event sequence number (1-based).</param>
/// <param name="Timestamp">When the event was persisted.</param>
/// <param name="CorrelationId">The correlation identifier for tracing.</param>
public record ProjectionEventDto(
    string EventTypeName,
    byte[] Payload,
    string SerializationFormat,
    long SequenceNumber,
    DateTimeOffset Timestamp,
    string CorrelationId);
