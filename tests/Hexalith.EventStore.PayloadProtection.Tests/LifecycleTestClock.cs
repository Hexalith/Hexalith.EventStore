namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>Deterministic monotonic time for cache lifetimes and breaker windows; real-sidecar proof uses real time.</summary>
internal sealed class LifecycleTestClock : TimeProvider
{
    private long _ticks;
    /// <summary>Gets the test clock tick frequency.</summary>
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;
    /// <summary>Gets elapsed monotonic ticks.</summary>
    public override long GetTimestamp() => Interlocked.Read(ref _ticks);
    /// <summary>Gets the corresponding UTC instant.</summary>
    public override DateTimeOffset GetUtcNow() => new DateTimeOffset(2026, 10, 23, 0, 0, 0, TimeSpan.Zero).AddTicks(GetTimestamp());
    /// <summary>Advances both clocks without waiting.</summary>
    internal void Advance(TimeSpan elapsed) => Interlocked.Add(ref _ticks, elapsed.Ticks);
}
