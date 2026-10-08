namespace Hexalith.EventStore.Client.Events;

/// <summary>Reserves the combined actual live capacities of one pipeline before buffer allocation.</summary>
internal sealed class EventBufferBudget : IDisposable
{
    private readonly int _maximumBytes;
    private int _liveBytes;
    private EventBufferReservation? _parent;
    private int _closed;

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
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _closed) != 0, this);
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
                    ObjectDisposedException.ThrowIf(Volatile.Read(ref _closed) != 0, this);
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

    /// <summary>Reserves one conservative future capacity before callbacks and lends allocations against that partition.</summary>
    internal EventBufferBudget CreatePartition(int capacity)
    {
        EventBufferReservation parent = Reserve(capacity);
        try
        {
            return new EventBufferBudget(capacity)
            {
                _parent = parent
            };
        }
        catch
        {
            parent.Dispose();
            throw;
        }
    }

    /// <summary>Releases a reservation after the owning buffer has been cleared.</summary>
    internal void Release(int capacity)
    {
        int live = Interlocked.Add(ref _liveBytes, -capacity);
        if (live == 0 && Volatile.Read(ref _closed) != 0)
        {
            Interlocked.Exchange(ref _parent, null)?.Dispose();
        }
    }

    /// <summary>Closes future allocations; an enclosing reservation remains until every surviving private owner clears.</summary>
    public void Dispose()
    {
        Interlocked.Exchange(ref _closed, 1);
        if (LiveBytes == 0)
        {
            Interlocked.Exchange(ref _parent, null)?.Dispose();
        }
    }
}
