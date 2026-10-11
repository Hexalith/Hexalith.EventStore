namespace Hexalith.EventStore.DomainService.Tests.Processes;

/// <summary>Deterministic time for process source lease boundary tests.</summary>
internal sealed class ManualProcessTimeProvider(DateTimeOffset now) : TimeProvider
{
    private DateTimeOffset _now = now;
    private long _timestamp;

    /// <inheritdoc/>
    public override DateTimeOffset GetUtcNow() => _now;

    /// <inheritdoc/>
    public override long GetTimestamp() => _timestamp;

    /// <inheritdoc/>
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    /// <summary>Moves time forward without a wall-clock wait.</summary>
    public void Advance(TimeSpan interval)
    {
        _now += interval;
        _timestamp += interval.Ticks;
    }

    /// <summary>Adjusts only the wall clock, leaving monotonic elapsed time unchanged.</summary>
    public void MoveWallClock(TimeSpan interval) => _now += interval;
}
