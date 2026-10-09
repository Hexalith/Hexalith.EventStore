using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Client.Tests.Events;

/// <summary>Historical version-one event with an Apply registration.</summary>
public sealed record LegacyTestEvent(int Amount) : IEventPayload;
