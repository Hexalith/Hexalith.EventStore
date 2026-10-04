using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Server.Events;

/// <summary>Represents an event payload received in serialized form from a domain service.</summary>
internal sealed record SerializedDomainEventPayload(
    string EventTypeName,
    byte[] PayloadBytes,
    string SerializationFormat,
    int? MetadataVersion,
    string? EventContractType,
    int? PayloadVersion) : ISerializedEventPayload;
