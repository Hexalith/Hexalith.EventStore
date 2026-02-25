using System.Text.Json;

using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Contracts.Results;

/// <summary>
/// Wire-safe response for domain service invocation.
/// Avoids interface-typed JSON deserialization issues by carrying event metadata explicitly.
/// </summary>
/// <param name="IsRejection">True when all emitted events are rejection events.</param>
/// <param name="Events">Serialized event payloads with explicit type names.</param>
public sealed record DomainServiceWireResult(
    bool IsRejection,
    IReadOnlyList<DomainServiceWireEvent> Events) {
    /// <summary>
    /// Converts a <see cref="DomainResult"/> into a wire-safe representation.
    /// </summary>
    /// <param name="result">The domain result to convert.</param>
    /// <returns>A wire-safe response containing explicit event metadata and payload bytes.</returns>
    public static DomainServiceWireResult FromDomainResult(DomainResult result) {
        ArgumentNullException.ThrowIfNull(result);

        var events = new List<DomainServiceWireEvent>(result.Events.Count);
        foreach (IEventPayload payload in result.Events) {
            string eventTypeName = payload.GetType().FullName ?? payload.GetType().Name;
            byte[] payloadBytes = JsonSerializer.SerializeToUtf8Bytes(payload, payload.GetType());
            events.Add(new DomainServiceWireEvent(eventTypeName, payloadBytes, "json"));
        }

        return new DomainServiceWireResult(result.IsRejection, events);
    }
}

/// <summary>
/// Serialized representation of a single domain event.
/// </summary>
/// <param name="EventTypeName">The fully-qualified event type name.</param>
/// <param name="Payload">Serialized event payload bytes.</param>
/// <param name="SerializationFormat">Payload serialization format (defaults to <c>json</c>).</param>
public sealed record DomainServiceWireEvent(
    string EventTypeName,
    byte[] Payload,
    string SerializationFormat = "json");
