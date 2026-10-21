using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Contracts.Tests.Events;

internal sealed record LegacySerializedPayload(
    string EventTypeName,
    byte[] PayloadBytes,
    string SerializationFormat) : ISerializedEventPayload;
