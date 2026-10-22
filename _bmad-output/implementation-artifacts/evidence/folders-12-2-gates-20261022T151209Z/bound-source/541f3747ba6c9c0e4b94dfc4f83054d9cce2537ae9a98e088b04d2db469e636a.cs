namespace Hexalith.EventStore.Client.Tests.Aggregates;

/// <summary>Models a getter that fails only after a successful Apply.</summary>
public sealed class UnserializableReplayState
{
    private bool _applied;

    /// <summary>Throws a payload-bearing error while serializing a changed working state.</summary>
    public int Count => _applied ? throw new InvalidOperationException("secret-payload-in-getter") : 0;

    /// <summary>Changes the state so canonical serialization cannot complete.</summary>
    public void Apply(AggregateReplayerTests.CounterIncremented payload) => _applied = true;
}
