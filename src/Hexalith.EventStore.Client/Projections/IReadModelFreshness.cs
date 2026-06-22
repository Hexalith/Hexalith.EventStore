namespace Hexalith.EventStore.Client.Projections;

/// <summary>
/// Optional metadata a persisted read model can expose so that consumers can classify its freshness
/// from a real persisted projection timestamp and/or version, instead of inferring it from transport
/// signals (ETags, degraded flags) or hand-rolling a per-domain timestamp field.
/// </summary>
/// <remarks>
/// <para>
/// A read model implements this interface to advertise <em>when</em> the projection that produced it
/// was last updated. The value is persisted as part of the read model itself (it is set by the
/// projection on every write and round-trips through <see cref="IReadModelStore"/>), so it survives
/// reads and reflects the true last-projected instant rather than the moment a gateway served a
/// response.
/// </para>
/// <para>
/// The interface is purely additive: existing read models that do not implement it keep working, and
/// freshness classification simply returns <see cref="ReadModelFreshnessState.Unknown"/> for them
/// (see <see cref="ReadModelFreshness.Classify(IReadModelFreshness?, ReadModelFreshnessThresholds, System.DateTimeOffset)"/>).
/// </para>
/// </remarks>
public interface IReadModelFreshness {
    /// <summary>
    /// Gets the UTC instant at which the projection that produced this read model was last updated,
    /// or <see langword="null"/> when the read model has never been projected.
    /// </summary>
    DateTimeOffset? ProjectedAt { get; }

    /// <summary>
    /// Gets an optional opaque projection version token (e.g. a monotonic sequence or a content hash)
    /// that consumers can compare to detect change without inspecting the payload. May be
    /// <see langword="null"/> when the projection does not track a version.
    /// </summary>
    string? ProjectionVersion { get; }
}
