namespace Hexalith.EventStore.Client.Projections;

/// <summary>
/// Pure, threshold-based freshness classification for persisted read models. Computes a
/// <see cref="ReadModelFreshnessState"/> from a real persisted projection timestamp and configured
/// aging/stale thresholds so that domains do not each hand-roll the comparison.
/// </summary>
/// <remarks>
/// All members are side-effect free and take an explicit <c>now</c>, so callers (and tests) control the
/// clock. The persisted timestamp comes from the read model itself via <see cref="IReadModelFreshness"/>
/// (set by the projection on write and round-tripped through <see cref="IReadModelStore"/>), not from
/// when a gateway served the response.
/// </remarks>
public static class ReadModelFreshness {
    /// <summary>
    /// Classifies a persisted projection age against aging/stale thresholds.
    /// </summary>
    /// <param name="projectedAt">
    /// The UTC instant the projection was last updated, or <see langword="null"/> when unknown.
    /// </param>
    /// <param name="thresholds">The aging/stale thresholds.</param>
    /// <param name="now">The current instant to measure age against.</param>
    /// <returns>
    /// <see cref="ReadModelFreshnessState.Unknown"/> when <paramref name="projectedAt"/> is
    /// <see langword="null"/>; otherwise <see cref="ReadModelFreshnessState.Current"/>,
    /// <see cref="ReadModelFreshnessState.Aging"/>, or <see cref="ReadModelFreshnessState.Stale"/> based
    /// on the age. A future timestamp (negative age, e.g. clock skew) is treated as
    /// <see cref="ReadModelFreshnessState.Current"/>.
    /// </returns>
    public static ReadModelFreshnessState Classify(
        DateTimeOffset? projectedAt,
        ReadModelFreshnessThresholds thresholds,
        DateTimeOffset now) {
        if (projectedAt is not { } at) {
            return ReadModelFreshnessState.Unknown;
        }

        TimeSpan age = now - at;
        if (age <= thresholds.Aging) {
            // Includes negative age (projection clock ahead of the reader); not stale.
            return ReadModelFreshnessState.Current;
        }

        return age <= thresholds.Stale
            ? ReadModelFreshnessState.Aging
            : ReadModelFreshnessState.Stale;
    }

    /// <summary>
    /// Classifies a persisted read model's freshness from the projection timestamp it exposes via
    /// <see cref="IReadModelFreshness"/>.
    /// </summary>
    /// <param name="readModel">
    /// The persisted read model, or <see langword="null"/> when the key is absent. A <see langword="null"/>
    /// read model (or one whose <see cref="IReadModelFreshness.ProjectedAt"/> is <see langword="null"/>)
    /// classifies as <see cref="ReadModelFreshnessState.Unknown"/>.
    /// </param>
    /// <param name="thresholds">The aging/stale thresholds.</param>
    /// <param name="now">The current instant to measure age against.</param>
    /// <returns>The freshness state.</returns>
    public static ReadModelFreshnessState Classify(
        IReadModelFreshness? readModel,
        ReadModelFreshnessThresholds thresholds,
        DateTimeOffset now) =>
        Classify(readModel?.ProjectedAt, thresholds, now);

    /// <summary>
    /// Returns the age of a persisted projection relative to <paramref name="now"/>, or
    /// <see langword="null"/> when the timestamp is unknown. Negative ages (future timestamps from clock
    /// skew) are clamped to <see cref="TimeSpan.Zero"/>.
    /// </summary>
    /// <param name="projectedAt">The UTC instant the projection was last updated, or <see langword="null"/>.</param>
    /// <param name="now">The current instant.</param>
    /// <returns>The non-negative age, or <see langword="null"/> when unknown.</returns>
    public static TimeSpan? Age(DateTimeOffset? projectedAt, DateTimeOffset now) {
        if (projectedAt is not { } at) {
            return null;
        }

        TimeSpan age = now - at;
        return age < TimeSpan.Zero ? TimeSpan.Zero : age;
    }
}
