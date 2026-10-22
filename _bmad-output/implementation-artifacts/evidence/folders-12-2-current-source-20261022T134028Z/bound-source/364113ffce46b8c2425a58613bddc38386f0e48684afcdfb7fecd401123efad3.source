using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Server.Tests.DomainServices;

/// <summary>Current event contract used by the actor JSON round-trip proof.</summary>
[EventPayloadVersion(2)]
internal sealed record VersionedActorEvent(int Value) : IEventPayload;
