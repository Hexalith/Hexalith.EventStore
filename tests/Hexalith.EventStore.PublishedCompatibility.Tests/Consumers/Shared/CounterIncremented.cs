using Hexalith.EventStore.Contracts.Events;

namespace PublishedConsumer;

/// <summary>A stable legacy event payload shared by every independently built consumer.</summary>
/// <param name="Amount">The increment amount.</param>
/// <param name="DueUtc">The optional committed reminder due instant.</param>
public sealed record CounterIncremented(int Amount = 1, DateTimeOffset? DueUtc = null) : IEventPayload;
