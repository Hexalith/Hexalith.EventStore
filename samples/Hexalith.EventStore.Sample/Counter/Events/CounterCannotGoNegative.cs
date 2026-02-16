namespace Hexalith.EventStore.Sample.Counter.Events;

using Hexalith.EventStore.Contracts.Events;

/// <summary>
/// Rejection event indicating the counter cannot be decremented below zero.
/// </summary>
public sealed record CounterCannotGoNegative : IRejectionEvent;
