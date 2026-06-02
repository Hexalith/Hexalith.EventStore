namespace Hexalith.EventStore.Client.Subscriptions;

/// <summary>
/// Wire-format DTO for domain events received via DAPR pub/sub.
/// </summary>
/// <remarks>
/// Matches the flat envelope published by the EventStore event publisher. Only the fields a consuming
/// service needs are declared; JSON deserialization ignores extras. Generalizes the per-domain consumer
/// envelopes domain modules previously hand-wrote (e.g. <c>TenantEventEnvelope</c>).
/// </remarks>
/// <param name="MessageId">The unique event message ID (ULID) used for idempotency.</param>
/// <param name="AggregateId">The aggregate identifier the event belongs to.</param>
/// <param name="TenantId">The tenant scope the event was published under.</param>
/// <param name="EventTypeName">The fully qualified .NET type name of the event payload.</param>
/// <param name="SequenceNumber">The event sequence number within the aggregate.</param>
/// <param name="Timestamp">When the event was persisted.</param>
/// <param name="CorrelationId">The request correlation ID for tracing.</param>
/// <param name="SerializationFormat">The serialization format (always <c>"json"</c>).</param>
/// <param name="Payload">The JSON-serialized event payload bytes.</param>
public record EventStoreDomainEventEnvelope(
    string MessageId,
    string AggregateId,
    string TenantId,
    string EventTypeName,
    long SequenceNumber,
    DateTimeOffset Timestamp,
    string CorrelationId,
    string SerializationFormat,
    byte[] Payload);
