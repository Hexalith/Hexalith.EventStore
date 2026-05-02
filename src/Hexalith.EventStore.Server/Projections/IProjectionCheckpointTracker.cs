using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>
/// Tracks the last aggregate event sequence delivered to the server-managed projection path.
/// </summary>
public interface IProjectionCheckpointTracker {
    /// <summary>
    /// Reads the last successfully delivered event sequence for an aggregate.
    /// </summary>
    /// <remarks>
    /// Reserved for incremental projection delivery. R11-A1b pinned full-replay as the production contract
    /// for both immediate and polling modes, so no in-tree caller invokes this in the delivery hot path.
    /// Kept on the interface for forward-compat with a future incremental-delivery design and for
    /// observability reads; do not introduce new callers without revisiting that design decision.
    /// </remarks>
    /// <param name="identity">The aggregate identity.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The last delivered sequence, or 0 when no checkpoint exists.</returns>
    Task<long> ReadLastDeliveredSequenceAsync(AggregateIdentity identity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a successful delivery checkpoint without lowering an existing higher sequence.
    /// </summary>
    /// <param name="identity">The aggregate identity.</param>
    /// <param name="deliveredSequence">The highest event sequence delivered successfully.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns><see langword="true"/> when the checkpoint was saved; otherwise <see langword="false"/>.</returns>
    Task<bool> SaveDeliveredSequenceAsync(AggregateIdentity identity, long deliveredSequence, CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers an aggregate identity as eligible for polling-mode projection delivery.
    /// </summary>
    /// <param name="identity">The aggregate identity.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task TrackIdentityAsync(AggregateIdentity identity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Enumerates tracked aggregate identities without depending on actor state key scans.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The tracked aggregate identities.</returns>
    IAsyncEnumerable<AggregateIdentity> EnumerateTrackedIdentitiesAsync(CancellationToken cancellationToken = default);
}
