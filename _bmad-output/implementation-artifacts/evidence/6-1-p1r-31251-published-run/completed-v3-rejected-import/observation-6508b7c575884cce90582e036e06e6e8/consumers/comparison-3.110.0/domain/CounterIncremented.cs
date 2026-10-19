using Hexalith.EventStore.Contracts.Events;

namespace P1R.Counter;

/// <summary>Disposable increment event.</summary>
public sealed record CounterIncremented : IEventPayload;
