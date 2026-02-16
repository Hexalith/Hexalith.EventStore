namespace Hexalith.EventStore.Sample.Counter.Events;

using Hexalith.EventStore.Contracts.Events;

/// <summary>
/// Event indicating the counter was decremented by one.
/// </summary>
public sealed record CounterDecremented : IEventPayload;
