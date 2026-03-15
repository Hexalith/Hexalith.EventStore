namespace Hexalith.EventStore.Contracts.Events;

/// <summary>
/// Contains the 15 typed metadata fields for an event (FR11). Enables structured access to metadata
/// without touching the payload (efficient for logging, indexing, routing).
/// </summary>
/// <param name="MessageId">The unique event message identifier (ULID).</param>
/// <param name="AggregateId">The aggregate identifier (ULID).</param>
/// <param name="AggregateType">The aggregate type name (e.g., "counter").</param>
/// <param name="TenantId">The tenant identifier.</param>
/// <param name="Domain">The domain name (bounded context).</param>
/// <param name="SequenceNumber">The event sequence number (starts at 1 per FR12).</param>
/// <param name="GlobalPosition">The cross-aggregate monotonic position (>= 0).</param>
/// <param name="Timestamp">The event timestamp (DateTimeOffset for timezone awareness).</param>
/// <param name="CorrelationId">The correlation identifier for request tracing (ULID).</param>
/// <param name="CausationId">The causation identifier linking to the originating command (ULID).</param>
/// <param name="UserId">The user who initiated the command.</param>
/// <param name="DomainServiceVersion">The version of the domain service that produced this event.</param>
/// <param name="EventTypeName">The fully qualified event type name.</param>
/// <param name="MetadataVersion">The metadata envelope schema version (>= 1 per FR65).</param>
/// <param name="SerializationFormat">The payload serialization format (e.g., "json").</param>
public record EventMetadata(
    string MessageId,
    string AggregateId,
    string AggregateType,
    string TenantId,
    string Domain,
    long SequenceNumber,
    long GlobalPosition,
    DateTimeOffset Timestamp,
    string CorrelationId,
    string CausationId,
    string UserId,
    string DomainServiceVersion,
    string EventTypeName,
    int MetadataVersion,
    string SerializationFormat) {
    /// <summary>Gets the event sequence number (must be >= 1 per FR12).</summary>
    public long SequenceNumber { get; } = SequenceNumber >= 1
        ? SequenceNumber
        : throw new ArgumentOutOfRangeException(nameof(SequenceNumber), SequenceNumber, "SequenceNumber must be >= 1 per FR12.");

    /// <summary>Gets the cross-aggregate monotonic position (must be >= 0).</summary>
    public long GlobalPosition { get; } = GlobalPosition >= 0
        ? GlobalPosition
        : throw new ArgumentOutOfRangeException(nameof(GlobalPosition), GlobalPosition, "GlobalPosition must be >= 0.");

    /// <summary>Gets the metadata envelope schema version (must be >= 1 per FR65).</summary>
    public int MetadataVersion { get; } = MetadataVersion >= 1
        ? MetadataVersion
        : throw new ArgumentOutOfRangeException(nameof(MetadataVersion), MetadataVersion, "MetadataVersion must be >= 1 per FR65.");
}
