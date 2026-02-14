namespace Hexalith.EventStore.Server.Events;

using Hexalith.EventStore.Contracts.Identity;

/// <summary>
/// Storage representation of a persisted event in the actor state store.
/// Contains 11 metadata fields plus the serialized payload.
/// </summary>
/// <param name="AggregateId">The aggregate identifier.</param>
/// <param name="TenantId">The tenant identifier.</param>
/// <param name="Domain">The domain name.</param>
/// <param name="SequenceNumber">The event sequence number (1-based).</param>
/// <param name="Timestamp">When the event was persisted.</param>
/// <param name="CorrelationId">The correlation identifier for tracing.</param>
/// <param name="CausationId">The causation identifier linking to the originating command.</param>
/// <param name="UserId">The user who triggered the command.</param>
/// <param name="DomainServiceVersion">The version of the domain service that produced the event.</param>
/// <param name="EventTypeName">The fully qualified event type name for deserialization.</param>
/// <param name="SerializationFormat">The serialization format (e.g., "json").</param>
/// <param name="Payload">The serialized event data.</param>
/// <param name="Extensions">Optional extension metadata.</param>
public record EventEnvelope(
    string AggregateId,
    string TenantId,
    string Domain,
    long SequenceNumber,
    DateTimeOffset Timestamp,
    string CorrelationId,
    string CausationId,
    string UserId,
    string DomainServiceVersion,
    string EventTypeName,
    string SerializationFormat,
    byte[] Payload,
    IDictionary<string, string>? Extensions)
{
    /// <summary>Gets the aggregate identity derived from this event's tenant, domain, and aggregate ID.</summary>
    public AggregateIdentity Identity => new(TenantId, Domain, AggregateId);
}
