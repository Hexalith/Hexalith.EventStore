using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Server.Events;

/// <summary>Represents a serialized rejection event received from a domain service.</summary>
internal sealed record SerializedDomainRejectionEventPayload(
    string EventTypeName,
    byte[] PayloadBytes,
    string SerializationFormat,
    int? MetadataVersion,
    string? EventContractType,
    int? PayloadVersion) : ISerializedEventPayload, IRejectionEvent;
