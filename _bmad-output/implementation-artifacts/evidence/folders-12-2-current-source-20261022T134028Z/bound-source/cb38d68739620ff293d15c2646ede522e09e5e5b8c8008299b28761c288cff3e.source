namespace Hexalith.EventStore.Client.Projections;

/// <summary>
/// Generic, threshold-based freshness classification for a persisted read model, derived from a real
/// persisted projection timestamp via <see cref="ReadModelFreshness"/>.
/// </summary>
/// <remarks>
/// <para>
/// This is the platform generalization of the per-domain freshness enums domain modules previously
/// hand-wrote (e.g. <c>TenantFreshnessState</c>). It lets consumers compute "aging"/"stale" states
/// generically from a single persisted projection timestamp instead of each domain re-deriving the
/// thresholds. The enum intentionally carries only the states a pure threshold computation can produce;
/// transient UI states such as a domain's "refreshing" are layered on top by the consumer and are not
/// emitted here.
/// </para>
/// </remarks>
public enum ReadModelFreshnessState {
    /// <summary>
    /// The persisted projection timestamp/version is unknown, so freshness cannot be classified
    /// (e.g. the read model does not expose a timestamp, or the key is absent).
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// The persisted projection is within the configured aging threshold and is considered up to date.
    /// </summary>
    Current = 1,

    /// <summary>
    /// The persisted projection age has crossed the aging threshold but not yet the stale threshold;
    /// it is still serviceable but should be surfaced as starting to age.
    /// </summary>
    Aging = 2,

    /// <summary>
    /// The persisted projection age has crossed the stale threshold and should be surfaced as stale.
    /// </summary>
    Stale = 3,
}
