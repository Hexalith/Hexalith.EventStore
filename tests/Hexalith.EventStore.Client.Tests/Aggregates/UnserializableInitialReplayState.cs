namespace Hexalith.EventStore.Client.Tests.Aggregates;

/// <summary>Models initial state whose registered serialization cannot complete.</summary>
public sealed class UnserializableInitialReplayState
{
    private int _count;

    /// <summary>Throws before a replay event can be applied.</summary>
    public int Count => _count == 0 ? throw new InvalidOperationException("secret-initial-state") : _count;

    /// <summary>Must not execute when canonical initial state is unavailable.</summary>
    public void Apply(AggregateReplayerTests.CounterIncremented payload)
        => _count++;
}
