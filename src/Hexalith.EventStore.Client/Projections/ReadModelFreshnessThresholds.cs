namespace Hexalith.EventStore.Client.Projections;

/// <summary>
/// The aging/stale thresholds used to classify a persisted read model's freshness from its persisted
/// projection age.
/// </summary>
/// <remarks>
/// A read model is <see cref="ReadModelFreshnessState.Current"/> while its age is at or below
/// <see cref="Aging"/>, <see cref="ReadModelFreshnessState.Aging"/> once its age exceeds
/// <see cref="Aging"/> but is at or below <see cref="Stale"/>, and
/// <see cref="ReadModelFreshnessState.Stale"/> once its age exceeds <see cref="Stale"/>.
/// </remarks>
/// <param name="Aging">
/// The age at which a read model transitions from <see cref="ReadModelFreshnessState.Current"/> to
/// <see cref="ReadModelFreshnessState.Aging"/>. Must be non-negative.
/// </param>
/// <param name="Stale">
/// The age at which a read model transitions to <see cref="ReadModelFreshnessState.Stale"/>. Must be
/// greater than or equal to <see cref="Aging"/>.
/// </param>
public readonly record struct ReadModelFreshnessThresholds(TimeSpan Aging, TimeSpan Stale) {
    /// <summary>
    /// Creates thresholds, validating that both are non-negative and that <paramref name="stale"/> is
    /// not earlier than <paramref name="aging"/>.
    /// </summary>
    /// <param name="aging">The aging threshold.</param>
    /// <param name="stale">The stale threshold.</param>
    /// <returns>The validated thresholds.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when a threshold is negative or <paramref name="stale"/> precedes <paramref name="aging"/>.
    /// </exception>
    public static ReadModelFreshnessThresholds Create(TimeSpan aging, TimeSpan stale) {
        ArgumentOutOfRangeException.ThrowIfLessThan(aging, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThan(stale, aging);
        return new ReadModelFreshnessThresholds(aging, stale);
    }

    internal void ThrowIfInvalid() {
        ArgumentOutOfRangeException.ThrowIfLessThan(Aging, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThan(Stale, Aging);
    }
}
