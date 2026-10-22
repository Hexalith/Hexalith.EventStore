using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Client.TestContracts;

/// <summary>A versioned event defined in an assembly separate from its processor.</summary>
/// <param name="Value">The current event value.</param>
[EventPayloadVersion(2)]
public sealed record ExternalVersionedEvent(int Value) : IEventPayload;
