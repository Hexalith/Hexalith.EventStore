namespace Hexalith.EventStore.Sample.Counter.Events;

using Hexalith.EventStore.Contracts.Events;

/// <summary>
/// Event indicating the counter was reset to zero.
/// </summary>
public sealed record CounterReset : IEventPayload;
