using Hexalith.EventStore.Sample.Counter.Events;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.DomainServices;

/// <summary>
/// Counter state used by the Dapr serialization round-trip proof aggregate.
/// </summary>
internal sealed class RoundTripCounterState
{
    /// <summary>
    /// Gets the current counter value.
    /// </summary>
    public int Count { get; private set; }

    /// <summary>
    /// Applies an increment event.
    /// </summary>
    /// <param name="event">The increment event.</param>
    public void Apply(CounterIncremented @event) => Count++;

    /// <summary>
    /// Applies a decrement event.
    /// </summary>
    /// <param name="event">The decrement event.</param>
    public void Apply(CounterDecremented @event) => Count--;

    /// <summary>
    /// Applies a reset event.
    /// </summary>
    /// <param name="event">The reset event.</param>
    public void Apply(CounterReset @event) => Count = 0;
}
