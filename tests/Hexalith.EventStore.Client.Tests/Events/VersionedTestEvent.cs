using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Client.Tests.Events;

/// <summary>Version-three event used to exercise JSON evolution.</summary>
[EventPayloadVersion(3)]
public sealed record VersionedTestEvent(int Value) : IEventPayload;
