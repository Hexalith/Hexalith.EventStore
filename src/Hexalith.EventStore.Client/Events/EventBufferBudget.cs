namespace Hexalith.EventStore.Client.Events;

/// <summary>Reserves the combined actual live capacities of one pipeline before buffer allocation.</summary>
internal sealed class EventBufferBudget
{
    private readonly int _maximumBytes;
    private int _liveBytes;

    /// <summary>Initializes a pipeline budget no larger than the approved 128 MiB scratch ceiling.</summary>
    internal EventBufferBudget(int maximumBytes = 128 * 1024 * 1024)
    {
        if (maximumBytes is < 0 or > 128 * 1024 * 1024)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumBytes));
        }

        _maximumBytes = maximumBytes;
    }

    /// <summary>Gets the capacity still charged to live private owners.</summary>
    internal int LiveBytes => Volatile.Read(ref _liveBytes);

    /// <summary>Atomically reserves capacity before allocation; release follows full owner clearing.</summary>
    internal EventBufferReservation Reserve(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(capacity);
        while (true)
        {
            int observed = Volatile.Read(ref _liveBytes);
            if (capacity > _maximumBytes - observed)
            {
                throw new InvalidOperationException("ScratchLimit: combined live buffer capacities exceed the pipeline budget.");
            }

            if (Interlocked.CompareExchange(ref _liveBytes, observed + capacity, observed) == observed)
            {
                try
                {
                    return new EventBufferReservation(this, capacity);
                }
                catch
                {
                    Release(capacity);
                    throw;
                }
            }
        }
    }

    /// <summary>Releases a reservation after the owning buffer has been cleared.</summary>
    internal void Release(int capacity) => _ = Interlocked.Add(ref _liveBytes, -capacity);
}
