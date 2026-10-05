namespace Hexalith.EventStore.Server.Events;

/// <summary>Owns one addressed, contiguous bounded page of resolved Dapr logical events.</summary>
internal sealed class DaprLogicalEventPage : IDisposable
{
    private readonly DaprLogicalEventView[] _views;

    /// <summary>Takes ownership of the already validated resolved event views.</summary>
    internal DaprLogicalEventPage(long startSequence, long actorHead, DaprLogicalEventView[] views)
    {
        StartSequence = startSequence;
        ActorHead = actorHead;
        _views = views;
        Events = Array.AsReadOnly(views);
    }

    /// <summary>Gets the first sequence requested from the actor.</summary>
    internal long StartSequence { get; }

    /// <summary>Gets the Dapr logical actor head observed before and after the page read.</summary>
    internal long ActorHead { get; }

    /// <summary>Gets the privately owned resolved events in sequence order.</summary>
    internal IReadOnlyList<DaprLogicalEventView> Events { get; }

    /// <inheritdoc/>
    public void Dispose()
    {
        foreach (DaprLogicalEventView view in _views)
        {
            view.Dispose();
        }
    }
}
