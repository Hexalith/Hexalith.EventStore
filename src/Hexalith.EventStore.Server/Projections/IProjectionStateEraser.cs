using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>
/// Coordinates erasure of caller-owned read models and the platform-owned aggregate delivery checkpoint.
/// </summary>
public interface IProjectionStateEraser {
    /// <summary>
    /// Attempts to erase every supplied read model and the aggregate delivery checkpoint as one logical operation.
    /// </summary>
    /// <remarks>
    /// When all targets share the checkpoint store, one atomic DAPR transaction deletes every key.
    /// Otherwise, read-model targets are erased in caller-supplied order and the checkpoint is erased
    /// last. A conflict or failure returns <see langword="false"/> and never reports partial completion
    /// as success. Retrying the same request is safe: each individual erase treats an absent key as
    /// success, so a retry resumes after any already-erased targets and converges to a fully erased state.
    /// </remarks>
    /// <param name="identity">The aggregate identity whose projection state is erased.</param>
    /// <param name="readModelTargets">The tenant-owned read-model keys and expected ETags.</param>
    /// <param name="checkpointEtag">The expected aggregate delivery checkpoint ETag.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>
    /// <see langword="true"/> only when all targets and the checkpoint are erased;
    /// otherwise <see langword="false"/>.
    /// </returns>
    Task<bool> TryEraseAsync(
        AggregateIdentity identity,
        IReadOnlyCollection<ReadModelEraseTarget> readModelTargets,
        string checkpointEtag,
        CancellationToken cancellationToken = default);
}
