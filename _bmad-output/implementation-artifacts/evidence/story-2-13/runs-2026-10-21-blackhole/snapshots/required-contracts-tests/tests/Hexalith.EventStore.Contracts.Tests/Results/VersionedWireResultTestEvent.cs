using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Contracts.Tests.Results;

/// <summary>A typed event whose wire producer must stamp version two.</summary>
[EventPayloadVersion(2)]
public sealed record VersionedWireResultTestEvent(int Value) : IEventPayload;
