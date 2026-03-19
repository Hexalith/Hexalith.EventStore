
using Hexalith.EventStore.Sample.Greeting.Events;

namespace Hexalith.EventStore.Sample.Greeting.State;

/// <summary>
/// Aggregate state for the Greeting domain. Tracks message count.
/// </summary>
public sealed class GreetingState {
    /// <summary>Gets the number of greetings sent.</summary>
    public int MessageCount { get; private set; }

    /// <summary>Applies a greeting sent event.</summary>
    public void Apply(GreetingSent e) => MessageCount++;
}
