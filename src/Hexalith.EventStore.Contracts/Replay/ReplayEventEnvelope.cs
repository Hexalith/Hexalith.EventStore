namespace Hexalith.EventStore.Contracts.Replay;

/// <summary>
/// Wire shape of a single persisted event passed to a domain replay endpoint. Carries
/// the metadata fields required to drive Apply via the runtime convention plus the
/// raw payload bytes. Intentionally narrower than the storage envelope: only fields
/// the Apply path or replay diagnostics consume.
/// </summary>
/// <param name="SequenceNumber">Stream sequence/version (>= 1). Replay sorts by this value.</param>
/// <param name="EventTypeName">Persisted event type name (drives Apply method resolution).</param>
/// <param name="Payload">Serialized event payload as raw bytes.</param>
/// <param name="SerializationFormat">Payload serialization format (e.g., "json"). Replay only supports json today.</param>
/// <param name="MetadataVersion">Metadata envelope schema version (>= 1).</param>
/// <param name="MessageId">Persisted event message identifier (ULID) for diagnostics.</param>
/// <param name="CorrelationId">Optional correlation id for diagnostics.</param>
/// <param name="CausationId">Optional causation id for diagnostics.</param>
public sealed record ReplayEventEnvelope(
    long SequenceNumber,
    string EventTypeName,
    byte[] Payload,
    string SerializationFormat,
    int MetadataVersion,
    string MessageId,
    string? CorrelationId,
    string? CausationId);
