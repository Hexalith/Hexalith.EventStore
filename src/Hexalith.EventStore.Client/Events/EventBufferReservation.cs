namespace Hexalith.EventStore.Client.Events;

/// <summary>Retains one capacity charge until its private owner has been fully cleared.</summary>
internal sealed class EventBufferReservation(EventBufferBudget budget, int capacity) : IDisposable
{
    private readonly object _gate = new();
    private EventBufferBudget? _budget = budget;
    private int _capacity = capacity;

    /// <summary>Releases only capacity proven unused after an admitted provider output's ownership and length are known.</summary>
    internal void ShrinkTo(int actualCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(actualCapacity);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_budget is null, this);
            if (actualCapacity > _capacity)
            {
                throw new ArgumentOutOfRangeException(nameof(actualCapacity), "A reservation can only shrink.");
            }

            _budget.Release(_capacity - actualCapacity);
            _capacity = actualCapacity;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        lock (_gate)
        {
            _budget?.Release(_capacity);
            _budget = null;
        }
    }
}
