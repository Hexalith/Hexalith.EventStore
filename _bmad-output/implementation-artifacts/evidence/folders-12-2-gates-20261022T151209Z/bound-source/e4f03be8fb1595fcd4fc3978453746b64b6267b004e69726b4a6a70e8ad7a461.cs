using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Client.Tests.Events.Other;

/// <summary>A same-short-name event for exact-name precedence tests.</summary>
public sealed record VersionedTestEvent(int Value) : IEventPayload;
