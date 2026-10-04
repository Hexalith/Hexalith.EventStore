using System.Text.Json.Serialization;

namespace Hexalith.EventStore.Contracts.Results;

/// <summary>
/// Serialized representation of a single domain event.
/// </summary>
/// <param name="EventTypeName">The stored event discriminator.</param>
/// <param name="Payload">Serialized event payload bytes.</param>
/// <param name="SerializationFormat">Payload serialization format (defaults to <c>json</c>).</param>
public sealed record DomainServiceWireEvent(
    string EventTypeName,
    byte[] Payload,
    string SerializationFormat = "json") {
    /// <summary>Gets the metadata envelope version for versioned events.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MetadataVersion { get; init; }

    /// <summary>Gets the canonical event contract type for versioned events.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? EventContractType { get; init; }

    /// <summary>Gets the payload schema version for versioned events.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? PayloadVersion { get; init; }
}
