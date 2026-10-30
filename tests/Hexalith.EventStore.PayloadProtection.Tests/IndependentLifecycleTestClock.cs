namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>Controls UTC and monotonic time separately to reproduce repeated-UTC lease renewals.</summary>
internal sealed class IndependentLifecycleTestClock : TimeProvider
{
    private long _timestamp;
    private DateTimeOffset _utc = new(2026, 10, 23, 0, 0, 0, TimeSpan.Zero);

    /// <summary>Gets the deterministic monotonic frequency.</summary>
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;
    /// <summary>Gets independently controlled monotonic time.</summary>
    public override long GetTimestamp() => _timestamp;
    /// <summary>Gets independently controlled UTC.</summary>
    public override DateTimeOffset GetUtcNow() => _utc;
    /// <summary>Advances monotonic time without advancing UTC.</summary>
    internal void AdvanceMonotonic(TimeSpan elapsed) => _timestamp += elapsed.Ticks;
    /// <summary>Advances UTC without advancing monotonic time.</summary>
    internal void AdvanceUtc(TimeSpan elapsed) => _utc += elapsed;
}
