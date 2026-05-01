using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>
/// Tracks the last aggregate event sequence delivered to the server-managed projection path.
/// </summary>
public interface IProjectionCheckpointTracker {
    /// <summary>
    /// Reads the last successfully delivered event sequence for an aggregate.
    /// </summary>
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
}
