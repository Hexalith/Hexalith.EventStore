using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Server.Tests.Events;

/// <summary>An event used to produce distinct histories with the same final folded state.</summary>
/// <param name="Value">The value applied to the state.</param>
public sealed record ConvergentSnapshotEvent(int Value) : IEventPayload;
