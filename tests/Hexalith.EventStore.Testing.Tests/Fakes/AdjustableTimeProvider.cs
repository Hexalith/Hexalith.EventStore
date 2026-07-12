namespace Hexalith.EventStore.Testing.Tests.Fakes;

/// <summary>
/// Minimal adjustable clock for deterministic in-memory fake expiration tests.
/// </summary>
/// <param name="utcNow">The initial UTC time.</param>
internal sealed class AdjustableTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    private DateTimeOffset _utcNow = utcNow;

    /// <inheritdoc/>
    public override DateTimeOffset GetUtcNow() => _utcNow;

    /// <summary>Advances the current time.</summary>
    /// <param name="duration">The duration to advance.</param>
    public void Advance(TimeSpan duration) => _utcNow += duration;
}
