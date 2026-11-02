namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>Supplies monotonic test time and an inspectable manually fired expiry timer.</summary>
internal sealed class CacheTimerClock : TimeProvider
{
    private long _ticks;

    /// <summary>Gets the clock frequency.</summary>
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    /// <summary>Gets the current monotonic timestamp.</summary>
    public override long GetTimestamp() => _ticks;

    /// <summary>Gets the current UTC time.</summary>
    public override DateTimeOffset GetUtcNow() => new DateTimeOffset(2026, 10, 23, 0, 0, 0, TimeSpan.Zero).AddTicks(_ticks);

    /// <summary>Gets the timer installed by the cache.</summary>
    internal CacheManualTimer Timer { get; private set; } = null!;

    /// <summary>Creates the manually fired timer.</summary>
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        Timer = new CacheManualTimer(callback, state);
        Timer.Change(dueTime, period);
        return Timer;
    }

    /// <summary>Advances the monotonic clock without invoking timer callbacks.</summary>
    internal void Advance(TimeSpan duration) => _ticks += duration.Ticks;
}
