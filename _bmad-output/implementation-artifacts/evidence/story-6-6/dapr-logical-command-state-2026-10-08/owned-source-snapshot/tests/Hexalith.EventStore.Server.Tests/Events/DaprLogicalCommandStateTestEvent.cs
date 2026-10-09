using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Server.Tests.Events;

/// <summary>Provides a distinct bounded producer input for callback refusal controls.</summary>
internal sealed record DaprLogicalCommandStateTestEvent : IEventPayload;
