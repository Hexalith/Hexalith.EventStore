namespace Hexalith.EventStore.Client.Tests.Aggregates;

/// <summary>Mutates a successful replay state before raising a later Apply failure.</summary>
public sealed class MutatingReplayFailureState
{
    /// <summary>Gets the working state's counter.</summary>
    public int Count { get; private set; }

    /// <summary>Applies a successful marker event.</summary>
    public void Apply(AggregateReplayerTests.CounterIncremented payload) => Count++;

    /// <summary>Damages the working state and then fails.</summary>
    public void Apply(AggregateReplayerTests.CounterDecremented payload)
    {
        Count = 999;
        throw new InvalidOperationException("Apply failed after mutation.");
    }
}
