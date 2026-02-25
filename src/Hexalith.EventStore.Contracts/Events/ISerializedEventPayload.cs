namespace Hexalith.EventStore.Contracts.Events;

/// <summary>
/// Marker for event payloads already serialized by a domain service.
/// Provides explicit event type metadata and payload bytes for transport-safe processing.
/// </summary>
public interface ISerializedEventPayload : IEventPayload {
    /// <summary>Gets the fully-qualified event type name emitted by the domain service.</summary>
    string EventTypeName { get; }

    /// <summary>Gets the serialized event payload bytes.</summary>
    byte[] PayloadBytes { get; }

    /// <summary>Gets the payload serialization format (e.g. <c>json</c>).</summary>
    string SerializationFormat { get; }
}
