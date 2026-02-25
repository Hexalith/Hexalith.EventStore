
using Hexalith.EventStore.Sample.Counter.Events;

namespace Hexalith.EventStore.Sample.Counter.State;
/// <summary>
/// Aggregate state for the Counter domain. Tracks the current count value
/// and applies events to reconstruct state from event replay.
/// </summary>
public sealed class CounterState {
    /// <summary>Gets the current count value.</summary>
    public int Count { get; private set; }

    /// <summary>Applies a counter incremented event.</summary>
    public void Apply(CounterIncremented e) => Count++;

    /// <summary>Applies a counter decremented event.</summary>
    public void Apply(CounterDecremented e) => Count--;

    /// <summary>Applies a counter reset event.</summary>
    public void Apply(CounterReset e) => Count = 0;
}
