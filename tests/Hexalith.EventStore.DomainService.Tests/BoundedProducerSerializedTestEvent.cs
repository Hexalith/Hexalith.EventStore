using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.DomainService.Tests;

/// <summary>Provides a separately declared payload fixture for bounded producer verification.</summary>
internal sealed record BoundedProducerSerializedTestEvent(byte[] PayloadBytes) : ISerializedEventPayload
{
    /// <inheritdoc/>
    public string EventTypeName => "legacy-exact-alias";
    /// <inheritdoc/>
    public string SerializationFormat => "json";
}
