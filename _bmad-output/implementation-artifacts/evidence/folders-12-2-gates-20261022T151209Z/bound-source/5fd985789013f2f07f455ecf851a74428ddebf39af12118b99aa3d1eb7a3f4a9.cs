namespace Hexalith.EventStore.Server.Tests.Security;

/// <summary>Manually advances the actual SDK deadline timer without waiting thirty wall-clock seconds.</summary>
/// <param name="now">The initial source-observation instant.</param>
internal sealed class RetainedHistoryTimeProvider(DateTimeOffset now) : TimeProvider
{
    private readonly List<RetainedHistoryTimer> _timers = [];
    private DateTimeOffset _now = now;

    /// <summary>Gets the last deadline duration requested by the SDK.</summary>
    public TimeSpan LastDueTime { get; private set; }

    /// <inheritdoc/>
    public override DateTimeOffset GetUtcNow() => _now;

    /// <inheritdoc/>
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    /// <inheritdoc/>
    public override long GetTimestamp() => _now.UtcTicks;

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
    public void Advance(TimeSpan elapsed)
    {
        _now += elapsed;
        foreach (RetainedHistoryTimer timer in _timers.ToArray())
        {
            timer.FireIfDue(_now);
        }
    }
}
