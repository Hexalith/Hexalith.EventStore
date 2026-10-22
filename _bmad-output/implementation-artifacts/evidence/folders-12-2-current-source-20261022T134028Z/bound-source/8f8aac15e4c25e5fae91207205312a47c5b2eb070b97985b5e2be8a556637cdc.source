namespace Hexalith.EventStore.Client.Tests.Streams;

/// <summary>Manually advances the actual SDK deadline timer without waiting thirty wall-clock seconds.</summary>
/// <param name="now">The initial source-observation instant.</param>
internal sealed class RetainedHistoryTimeProvider(DateTimeOffset now) : TimeProvider
{
    private readonly List<RetainedHistoryTimer> _timers = [];
    private long _ticks;

    /// <summary>Gets the last deadline duration requested by the SDK.</summary>
    public TimeSpan LastDueTime { get; private set; }

    /// <inheritdoc/>
    public override DateTimeOffset GetUtcNow() => now.AddTicks(Interlocked.Read(ref _ticks));

    /// <inheritdoc/>
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    /// <inheritdoc/>
    public override long GetTimestamp() => Interlocked.Read(ref _ticks);

    /// <inheritdoc/>
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        LastDueTime = dueTime;
        var timer = new RetainedHistoryTimer(this, callback, state);
        timer.Change(dueTime, period);
        _timers.Add(timer);
        return timer;
    }

    /// <summary>Moves source time and fires every timer due at the resulting instant.</summary>
    public void Advance(TimeSpan elapsed, bool fireTimers = true)
    {
        Interlocked.Add(ref _ticks, elapsed.Ticks);
        if (!fireTimers) { return; }
        foreach (RetainedHistoryTimer timer in _timers.ToArray())
        {
            timer.FireIfDue(GetUtcNow());
        }
    }
}
