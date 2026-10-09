using System.Text.Json;

using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Contracts.Results;

/// <summary>
/// Wire-safe response for domain service invocation.
/// Avoids interface-typed JSON deserialization issues by carrying event metadata explicitly.
/// </summary>
/// <param name="IsRejection">True when all emitted events are rejection events.</param>
/// <param name="Events">Serialized event payloads with explicit type names.</param>
/// <param name="ResultPayload">Optional serialized payload for enriched successful command results.</param>
public sealed record DomainServiceWireResult(
    bool IsRejection,
    IReadOnlyList<DomainServiceWireEvent> Events,
    string? ResultPayload = null) {
    /// <summary>Gets the event writer mode echoed by a version-aware domain service.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? WriterMode { get; init; }

    /// <summary>Gets the event registry fingerprint echoed by a version-aware domain service.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? RegistryFingerprint { get; init; }

    /// <summary>
    /// Converts a <see cref="DomainResult"/> into a wire-safe representation.
    /// </summary>
    /// <param name="result">The domain result to convert.</param>
    /// <returns>A wire-safe response containing explicit event metadata and payload bytes.</returns>
    public static DomainServiceWireResult FromDomainResult(DomainResult result) {
        ArgumentNullException.ThrowIfNull(result);

        var events = new List<DomainServiceWireEvent>(result.Events.Count);
        foreach (IEventPayload payload in result.Events) {
            Type payloadType = payload.GetType();
            int version = EventPayloadVersionResolver.GetDeclaredVersion(payloadType);
            if (payload is ISerializedEventPayload serialized && (serialized.PayloadVersion ?? 1) != version)
            {
                throw new InvalidOperationException($"Serialized event {payloadType.FullName} version {serialized.PayloadVersion ?? 1} differs from declared version {version}.");
            }
            string eventTypeName = payload is ISerializedEventPayload source
                ? source.EventTypeName
                : payloadType.FullName ?? payloadType.Name;
            byte[] payloadBytes = payload is ISerializedEventPayload bytes
                ? bytes.PayloadBytes
                : JsonSerializer.SerializeToUtf8Bytes(payload, payloadType);
            string format = payload is ISerializedEventPayload formatted ? formatted.SerializationFormat : "json";
            if (version > 1 && !string.Equals(format, "json", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Versioned event {payloadType.FullName} must use JSON.");
            }
            events.Add(new DomainServiceWireEvent(eventTypeName, payloadBytes, format)
            {
                PayloadVersion = version == 1 ? null : version,
            });
        }

        string? resultPayload = result.IsSuccess || result.IsNoOp ? result.ResultPayload : null;
        return new DomainServiceWireResult(result.IsRejection, events, resultPayload);
    }
}
