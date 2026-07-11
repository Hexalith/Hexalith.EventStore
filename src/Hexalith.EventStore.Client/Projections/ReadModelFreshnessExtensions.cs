using Hexalith.EventStore.Contracts.Queries;

namespace Hexalith.EventStore.Client.Projections;

/// <summary>
/// Convenience bridges that connect a persisted read model's freshness metadata
/// (<see cref="IReadModelFreshness"/>) to the <see cref="IReadModelStore"/> read path and to the public
/// query-response metadata contract (<see cref="QueryResponseMetadata"/>), so a domain query handler can
/// surface a real persisted projection timestamp/version without hand-rolling the wiring.
/// </summary>
public static class ReadModelFreshnessExtensions {
    /// <summary>
    /// Reads a freshness-aware read model and classifies its freshness against the supplied thresholds.
    /// </summary>
    /// <typeparam name="TValue">The read-model type, which exposes its persisted projection timestamp.</typeparam>
    /// <param name="store">The read-model store.</param>
    /// <param name="storeName">The DAPR state-store component name.</param>
    /// <param name="key">The state key.</param>
    /// <param name="thresholds">The aging/stale thresholds.</param>
    /// <param name="now">The current instant to measure age against.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The read entry together with its classified freshness state.</returns>
    public static async Task<ReadModelFreshnessResult<TValue>> GetWithFreshnessAsync<TValue>(
        this IReadModelStore store,
        string storeName,
        string key,
        ReadModelFreshnessThresholds thresholds,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
        where TValue : class, IReadModelFreshness {
        ArgumentNullException.ThrowIfNull(store);

        ReadModelEntry<TValue> entry = await store
            .GetAsync<TValue>(storeName, key, cancellationToken)
            .ConfigureAwait(false);

        ReadModelFreshnessState state = ReadModelFreshness.Classify(entry.Value, thresholds, now);
        return new ReadModelFreshnessResult<TValue>(entry.Value, entry.ETag, state);
    }

    /// <summary>
    /// Projects a persisted read model's freshness metadata into the public
    /// <see cref="QueryResponseMetadata"/> contract, filling <see cref="QueryResponseMetadata.Lifecycle"/>,
    /// <see cref="QueryResponseMetadata.IsStale"/>, <see cref="QueryResponseMetadata.ProjectionVersion"/>,
    /// and <see cref="QueryResponseMetadata.ServedAt"/> from a real persisted projection timestamp/version.
    /// </summary>
    /// <param name="readModel">
    /// The persisted read model, or <see langword="null"/> when the key is absent (treated as
    /// <see cref="ReadModelFreshnessState.Unknown"/>).
    /// </param>
    /// <param name="thresholds">The aging/stale thresholds.</param>
    /// <param name="now">The current instant, also used as <see cref="QueryResponseMetadata.ServedAt"/>.</param>
    /// <param name="eTag">The optional ETag to carry on the metadata.</param>
    /// <returns>
    /// Query response metadata whose <see cref="QueryResponseMetadata.Lifecycle"/> is
    /// <see cref="ProjectionLifecycleState.Current"/> for current or aging evidence,
    /// <see cref="ProjectionLifecycleState.Stale"/> for stale evidence, and
    /// <see cref="ProjectionLifecycleState.Unknown"/> when freshness evidence is absent. The compatible
    /// <see cref="QueryResponseMetadata.IsStale"/> value is <see langword="true"/> only for stale evidence,
    /// <see langword="false"/> for current or aging evidence, and <see langword="null"/> when unknown.
    /// </returns>
    public static QueryResponseMetadata ToQueryResponseMetadata(
        this IReadModelFreshness? readModel,
        ReadModelFreshnessThresholds thresholds,
        DateTimeOffset now,
        string? eTag = null) {
        ReadModelFreshnessState state = ReadModelFreshness.Classify(readModel, thresholds, now);
        bool? isStale = state switch {
            ReadModelFreshnessState.Stale => true,
            ReadModelFreshnessState.Unknown => null,
            _ => false,
        };
        ProjectionLifecycleState lifecycle = state switch {
            ReadModelFreshnessState.Current or ReadModelFreshnessState.Aging => ProjectionLifecycleState.Current,
            ReadModelFreshnessState.Stale => ProjectionLifecycleState.Stale,
            _ => ProjectionLifecycleState.Unknown,
        };

        return new QueryResponseMetadata(
            ETag: eTag,
            IsStale: isStale,
            ProjectionVersion: readModel?.ProjectionVersion,
            ServedAt: now) {
            Lifecycle = lifecycle,
        };
    }
}
