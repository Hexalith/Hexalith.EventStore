using System.Text.Json.Serialization;

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
    string? CausationId) {
    /// <summary>Gets the canonical event contract identity when carried by a versioned source.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? StoredEventContractType { get; init; }

    /// <summary>Gets the payload schema version when carried by a versioned source.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? StoredPayloadVersion { get; init; }

    /// <summary>Gets the stored payload format provenance; transport binding conveys no verification authority.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? StoredSerializationFormat { get; init; }

    /// <summary>Gets the untrusted effective event identity hint; transport binding conveys no verification authority.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? EffectiveEventContractType { get; init; }

    /// <summary>Gets the untrusted effective payload version hint; transport binding conveys no verification authority.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? EffectivePayloadVersion { get; init; }

    /// <summary>Gets the untrusted effective payload format hint; transport binding conveys no verification authority.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? EffectiveSerializationFormat { get; init; }

    /// <summary>Gets a transport copy of the untrusted effective payload hint; transport binding conveys no verification authority.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public byte[]? EffectivePayload { get; init; }

    /// <summary>Gets the original stored discriminator provenance; transport binding conveys no verification authority.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? StoredEventTypeName { get; init; }

    /// <summary>Gets a transport copy of the stored digest hint; transport binding conveys no verification authority.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public byte[]? StoredDigest { get; init; }

    /// <summary>Gets the untrusted registry fingerprint hint; transport binding conveys no verification authority.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RegistryFingerprint { get; init; }

    /// <summary>Gets the untrusted adaptation hint; transport binding conveys no verification authority.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IsAdapted { get; init; }
}
