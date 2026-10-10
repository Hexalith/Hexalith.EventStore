using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Contracts.Tests.Results;

/// <summary>A typed event whose wire producer must remain unstamped.</summary>
public sealed record LegacyWireResultTestEvent(int Value) : IEventPayload;
