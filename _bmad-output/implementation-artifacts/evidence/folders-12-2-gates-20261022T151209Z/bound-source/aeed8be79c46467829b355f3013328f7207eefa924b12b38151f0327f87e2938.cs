namespace Hexalith.EventStore.Contracts.Aggregates;

/// <summary>
/// Opt-in interface for aggregate states that support tombstoning (FR66).
/// When <see cref="IsTerminated"/> is <c>true</c>, <c>EventStoreAggregate.ProcessAsync</c>
/// rejects all subsequent commands with an <c>AggregateTerminated</c> rejection event.
/// </summary>
/// <remarks>
/// States implementing this interface MUST also provide a no-op <c>Apply(AggregateTerminated)</c> method,
/// because the framework persists <c>AggregateTerminated</c> rejection events to the event stream
/// and rehydration replays all events.
/// </remarks>
public interface ITerminatable {
    /// <summary>Gets a value indicating whether this aggregate has been permanently terminated.</summary>
    bool IsTerminated { get; }
}
