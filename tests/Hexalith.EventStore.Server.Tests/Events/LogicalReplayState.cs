namespace Hexalith.EventStore.Server.Tests.Events;

/// <summary>Folds the fixture's legacy events through the actual Apply convention.</summary>
public sealed class LogicalReplayState
{
    /// <summary>Gets the accumulated event count.</summary>
    public int Count { get; private set; }

    /// <summary>Applies one admitted legacy event.</summary>
    public void Apply(Legacy.Event value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Count += value.Count;
    }
}
