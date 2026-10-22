
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Hexalith.EventStore.Contracts.Projections;

/// <summary>
/// Wire-format event DTO sent to domain services for projection building.
/// Deliberately excludes Server-internal fields (CausationId, DomainServiceVersion,
/// Extensions, AggregateId/Type/TenantId/Domain)
/// to maintain the security boundary.
/// </summary>
[DataContract]
public record ProjectionEventDto {
    private long _globalPosition;

    /// <summary>Initializes the legacy eight-member projection event shape.</summary>
    [OverloadResolutionPriority(1)]
    public ProjectionEventDto(
        string EventTypeName,
        byte[] Payload,
        string SerializationFormat,
        long SequenceNumber,
        DateTimeOffset Timestamp,
        string CorrelationId,
        string? MessageId = null,
        string? UserId = null)
        : this(
            EventTypeName,
            Payload,
            SerializationFormat,
            SequenceNumber,
            Timestamp,
            CorrelationId,
            MessageId,
            UserId,
            GlobalPosition: 0) {
    }

    /// <summary>Initializes a projection event carrying its exact persisted global position.</summary>
    [JsonConstructor]
    public ProjectionEventDto(
        string EventTypeName,
        byte[] Payload,
        string SerializationFormat,
        long SequenceNumber,
        DateTimeOffset Timestamp,
        string CorrelationId,
        string? MessageId = null,
        string? UserId = null,
        long GlobalPosition = 0) {
        this.EventTypeName = EventTypeName;
        this.Payload = Payload;
        this.SerializationFormat = SerializationFormat;
        this.SequenceNumber = SequenceNumber;
        this.Timestamp = Timestamp;
        this.CorrelationId = CorrelationId;
        this.MessageId = MessageId;
        this.UserId = UserId;
        this.GlobalPosition = GlobalPosition;
    }

    /// <summary>Gets the fully qualified event type name for deserialization.</summary>
    [DataMember]
    public string EventTypeName { get; init; }

    /// <summary>Gets the serialized event data.</summary>
    [DataMember]
    public byte[] Payload { get; init; }

    /// <summary>Gets the serialization format, such as <c>json</c>.</summary>
    [DataMember]
    public string SerializationFormat { get; init; }

    /// <summary>Gets the one-based aggregate event sequence number.</summary>
    [DataMember]
    public long SequenceNumber { get; init; }

    /// <summary>Gets when the event was persisted.</summary>
    [DataMember]
    public DateTimeOffset Timestamp { get; init; }

    /// <summary>Gets the correlation identifier used for tracing.</summary>
    [DataMember]
    public string CorrelationId { get; init; }

    /// <summary>Gets the unique persisted event message identifier, when available.</summary>
    [DataMember]
    public string? MessageId { get; init; }

    /// <summary>Gets the actor user identifier that produced the event, when available.</summary>
    [DataMember]
    public string? UserId { get; init; }

    /// <summary>Gets the stored metadata version when explicitly carried by the sender.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [DataMember]
    public int? MetadataVersion { get; init; }

    /// <summary>Gets the canonical event contract identity when carried by a versioned source.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [DataMember]
    public string? StoredEventContractType { get; init; }

    /// <summary>Gets the payload schema version when carried by a versioned source.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [DataMember]
    public int? StoredPayloadVersion { get; init; }

    /// <summary>Gets the stored payload format provenance; transport binding conveys no verification authority.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [DataMember]
    public string? StoredSerializationFormat { get; init; }

    /// <summary>Gets the untrusted effective event identity hint; transport binding conveys no verification authority.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [DataMember]
    public string? EffectiveEventContractType { get; init; }

    /// <summary>Gets the untrusted effective payload version hint; transport binding conveys no verification authority.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [DataMember]
    public int? EffectivePayloadVersion { get; init; }

    /// <summary>Gets the untrusted effective payload format hint; transport binding conveys no verification authority.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [DataMember]
    public string? EffectiveSerializationFormat { get; init; }

    /// <summary>Gets a transport copy of the untrusted effective payload hint; transport binding conveys no verification authority.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [DataMember]
    public byte[]? EffectivePayload { get; init; }

    /// <summary>Gets the original stored discriminator provenance; transport binding conveys no verification authority.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [DataMember]
    public string? StoredEventTypeName { get; init; }

    /// <summary>Gets a transport copy of the stored digest hint; transport binding conveys no verification authority.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [DataMember]
    public byte[]? StoredDigest { get; init; }

    /// <summary>Gets the untrusted registry fingerprint hint; transport binding conveys no verification authority.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [DataMember]
    public string? RegistryFingerprint { get; init; }

    /// <summary>Gets the untrusted adaptation hint; transport binding conveys no verification authority.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [DataMember]
    public bool? IsAdapted { get; init; }

    /// <summary>
    /// Gets the exact persisted cross-aggregate position. Zero represents a legacy event for
    /// which no authoritative persisted position is available; positive values may contain gaps.
    /// </summary>
    /// <remarks>
    /// A projection may persist the highest positive value in a delivered event slice as its
    /// watermark only as part of the same successful durable read-model write. The value does not
    /// assert contiguous consumption of every global position.
    /// </remarks>
    [DataMember]
    public long GlobalPosition {
        get => _globalPosition;
        init => _globalPosition = value >= 0
            ? value
            : throw new ArgumentOutOfRangeException(
                nameof(GlobalPosition),
                value,
                "GlobalPosition must be zero (legacy/unknown) or a positive persisted position.");
    }

    /// <summary>Deconstructs the legacy eight-member projection event shape.</summary>
    public void Deconstruct(
        out string EventTypeName,
        out byte[] Payload,
        out string SerializationFormat,
        out long SequenceNumber,
        out DateTimeOffset Timestamp,
        out string CorrelationId,
        out string? MessageId,
        out string? UserId) {
        EventTypeName = this.EventTypeName;
        Payload = this.Payload;
        SerializationFormat = this.SerializationFormat;
        SequenceNumber = this.SequenceNumber;
        Timestamp = this.Timestamp;
        CorrelationId = this.CorrelationId;
        MessageId = this.MessageId;
        UserId = this.UserId;
    }

    /// <summary>Deconstructs the projection event shape including its persisted global position.</summary>
    public void Deconstruct(
        out string EventTypeName,
        out byte[] Payload,
        out string SerializationFormat,
        out long SequenceNumber,
        out DateTimeOffset Timestamp,
        out string CorrelationId,
        out string? MessageId,
        out string? UserId,
        out long GlobalPosition) {
        Deconstruct(
            out EventTypeName,
            out Payload,
            out SerializationFormat,
            out SequenceNumber,
            out Timestamp,
            out CorrelationId,
            out MessageId,
            out UserId);
        GlobalPosition = this.GlobalPosition;
    }
}
