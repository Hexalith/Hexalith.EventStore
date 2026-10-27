using Hexalith.EventStore.Contracts.Events;

namespace PublishedConsumer;

/// <summary>A stable legacy event payload shared by every independently built consumer.</summary>
public sealed record CounterIncremented(int Amount = 1, DateTimeOffset? DueUtc = null) : IEventPayload;
