namespace Hexalith.EventStore.Client.Events;

/// <summary>Retains one capacity charge until its private owner has been fully cleared.</summary>
internal sealed class EventBufferReservation(EventBufferBudget budget, int capacity) : IDisposable
{
    private EventBufferBudget? _budget = budget;

    /// <inheritdoc/>
    public void Dispose() => Interlocked.Exchange(ref _budget, null)?.Release(capacity);
}
