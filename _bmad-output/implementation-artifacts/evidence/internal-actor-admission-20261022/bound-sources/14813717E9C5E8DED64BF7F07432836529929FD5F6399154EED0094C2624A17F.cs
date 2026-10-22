using System.Text.Json.Serialization;

using Hexalith.EventStore.Contracts.Events;

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
    byte[] Payload) {
    /// <summary>Gets the EventStore domain that published the event, when present in the publisher envelope.</summary>
    public string? Domain { get; init; }

    /// <summary>Gets the global stream position, when present in the publisher envelope.</summary>
    public long? GlobalPosition { get; init; }

    /// <summary>Gets the causation identifier, when present in the publisher envelope.</summary>
    public string? CausationId { get; init; }

    /// <summary>Gets the user identifier that produced the event, when present in the publisher envelope.</summary>
    public string? UserId { get; init; }

    /// <summary>Gets the stored metadata version when supplied by the publisher.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MetadataVersion { get; init; }

    /// <summary>Gets the canonical event contract type when supplied by a versioned publisher.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? EventContractType { get; init; }

    /// <summary>Gets the payload schema version when supplied by a versioned publisher.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? PayloadVersion { get; init; }

    /// <summary>Gets the stored aggregate type when supplied by the publisher; transport binding conveys no verification authority.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AggregateType { get; init; }

    /// <summary>Gets the producer software version when supplied by the publisher; transport binding conveys no verification authority.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DomainServiceVersion { get; init; }

    /// <summary>Gets the stored canonical event identity provenance; transport binding conveys no verification authority.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? StoredEventContractType { get; init; }

    /// <summary>Gets the stored payload schema version provenance; transport binding conveys no verification authority.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? StoredPayloadVersion { get; init; }

    /// <summary>Gets the untrusted readable payload format hint; transport binding conveys no verification authority.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ReadableSerializationFormat { get; init; }

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

    /// <summary>Gets the untrusted readable extension hints; transport binding conveys no verification authority.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? ReadableExtensions { get; init; }

    /// <summary>Gets the untrusted effective event transport hint; transport binding conveys no verification authority.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public VerifiedEffectiveEventView? VerifiedEffectiveEvent { get; init; }
}
