namespace Hexalith.EventStore.Client.Tests.Streams;

/// <summary>Manual monotonic clock with independently delayable SDK timer delivery.</summary>
internal sealed class AuthoritativeReadTimeProvider : TimeProvider
{
    private readonly List<RetainedHistoryTimer> _timers = [];
    private long _ticks;

    /// <inheritdoc/>
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    /// <inheritdoc/>
    public override long GetTimestamp() => Interlocked.Read(ref _ticks);

    /// <inheritdoc/>
    public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch.AddTicks(GetTimestamp());

    /// <inheritdoc/>
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new RetainedHistoryTimer(this, callback, state);
        timer.Change(dueTime, period);
        _timers.Add(timer);
        return timer;
    }

    /// <summary>Moves monotonic time; callbacks may deliberately be delayed.</summary>
    public void Advance(TimeSpan elapsed, bool fireTimers = true)
    {
        Interlocked.Add(ref _ticks, elapsed.Ticks);
        if (fireTimers)
        {
            foreach (RetainedHistoryTimer timer in _timers.ToArray()) { timer.FireIfDue(GetUtcNow()); }
        }
    }
}
