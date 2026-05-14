
namespace Hexalith.EventStore.Contracts.Projections;

/// <summary>
/// Wire-format event DTO sent to domain services for projection building.
/// Deliberately excludes Server-internal fields (CausationId, DomainServiceVersion,
/// GlobalPosition, MetadataVersion, Extensions, AggregateId/Type/TenantId/Domain)
/// to maintain the security boundary.
/// </summary>
/// <param name="EventTypeName">The fully qualified event type name for deserialization.</param>
/// <param name="Payload">The serialized event data.</param>
/// <param name="SerializationFormat">The serialization format (e.g., "json").</param>
/// <param name="SequenceNumber">The event sequence number (1-based).</param>
/// <param name="Timestamp">When the event was persisted.</param>
/// <param name="CorrelationId">The correlation identifier for tracing.</param>
/// <param name="MessageId">The unique persisted event message identifier, when available.</param>
/// <param name="UserId">The actor user identifier that produced the event, when available.</param>
public record ProjectionEventDto(
    string EventTypeName,
    byte[] Payload,
    string SerializationFormat,
    long SequenceNumber,
    DateTimeOffset Timestamp,
    string CorrelationId,
    string? MessageId = null,
    string? UserId = null);
