namespace Hexalith.EventStore.Contracts.Events;

/// <summary>
/// Contains the 11 typed metadata fields for an event. Enables structured access to metadata
/// without touching the payload (efficient for logging, indexing, routing).
/// </summary>
/// <param name="AggregateId">The aggregate identifier.</param>
/// <param name="TenantId">The tenant identifier.</param>
/// <param name="Domain">The domain name.</param>
/// <param name="SequenceNumber">The event sequence number (starts at 1 per FR12).</param>
/// <param name="Timestamp">The event timestamp (DateTimeOffset for timezone awareness).</param>
/// <param name="CorrelationId">The correlation identifier for request tracing.</param>
/// <param name="CausationId">The causation identifier linking to the originating command.</param>
/// <param name="UserId">The user who initiated the command.</param>
/// <param name="DomainServiceVersion">The version of the domain service that produced this event.</param>
/// <param name="EventTypeName">The fully qualified event type name.</param>
/// <param name="SerializationFormat">The payload serialization format (e.g., "json").</param>
public record EventMetadata(
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
    string SerializationFormat) {
    /// <summary>Gets the event sequence number (must be >= 1 per FR12).</summary>
    public long SequenceNumber { get; } = SequenceNumber >= 1
        ? SequenceNumber
        : throw new ArgumentOutOfRangeException(nameof(SequenceNumber), SequenceNumber, "SequenceNumber must be >= 1 per FR12.");
}
