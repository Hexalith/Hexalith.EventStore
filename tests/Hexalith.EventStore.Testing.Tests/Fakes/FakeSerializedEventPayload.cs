using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Testing.Tests.Fakes;

/// <summary>Represents already serialized input to the event persister.</summary>
/// <param name="EventTypeName">The supplied event name.</param>
/// <param name="PayloadBytes">The supplied event bytes.</param>
/// <param name="SerializationFormat">The supplied payload format.</param>
internal sealed record FakeSerializedEventPayload(string EventTypeName, byte[] PayloadBytes, string SerializationFormat)
    : ISerializedEventPayload;
