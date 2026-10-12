namespace Hexalith.EventStore.DomainService.Tests.Processes;

/// <summary>Deterministic time for process source lease boundary tests.</summary>
internal sealed class ManualProcessTimeProvider(DateTimeOffset now) : TimeProvider
{
    private DateTimeOffset _now = now;
    private long _timestamp;
    private readonly object _sync = new();
    private readonly List<ManualProcessTimer> _timers = [];

    /// <summary>The delay requested by the latest registered runtime timer.</summary>
    public TimeSpan LastDueTime { get; private set; }

    /// <inheritdoc/>
    public override DateTimeOffset GetUtcNow()
    {
        lock (_sync) return _now;
    }

    /// <inheritdoc/>
    public override long GetTimestamp()
    {
        lock (_sync) return _timestamp;
    }

    /// <inheritdoc/>
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    /// <inheritdoc/>
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        ManualProcessTimer timer = new(this, callback, state);
        _ = timer.Change(dueTime, period);
        lock (_sync)
        {
            LastDueTime = dueTime;
            _timers.Add(timer);
        }
        return timer;
    }

    /// <summary>Moves time forward without a wall-clock wait.</summary>
    public void Advance(TimeSpan interval)
    {
        ManualProcessTimer[] timers;
        DateTimeOffset current;
        lock (_sync)
        {
            _now += interval;
            _timestamp += interval.Ticks;
            current = _now;
            timers = [.. _timers];
        }
        foreach (ManualProcessTimer timer in timers) timer.FireIfDue(current);
    }

    /// <summary>Adjusts only the wall clock, leaving monotonic elapsed time unchanged.</summary>
    public void MoveWallClock(TimeSpan interval)
    {
        lock (_sync) _now += interval;
    }
}
