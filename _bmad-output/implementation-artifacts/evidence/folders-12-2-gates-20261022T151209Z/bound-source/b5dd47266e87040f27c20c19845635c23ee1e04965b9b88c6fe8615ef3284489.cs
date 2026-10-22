namespace Hexalith.EventStore.Client.Tests.Aggregates;

/// <summary>Supplies a deterministic monotonic page-lease clock.</summary>
internal sealed class ManualReplayTimeProvider : TimeProvider
{
    private long _timestamp;

    /// <inheritdoc/>
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    /// <inheritdoc/>
    public override long GetTimestamp() => _timestamp;

    /// <summary>Moves the lease clock without sleeping.</summary>
    internal void Advance(TimeSpan elapsed) => _timestamp += elapsed.Ticks;
}
