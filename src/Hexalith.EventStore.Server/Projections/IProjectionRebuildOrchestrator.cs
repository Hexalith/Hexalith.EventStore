namespace Hexalith.EventStore.Server.Projections;

/// <summary>
/// Runs operator-triggered projection rebuild work and advances rebuild checkpoints only after accepted projection applies.
/// </summary>
public interface IProjectionRebuildOrchestrator {
    /// <summary>
    /// Rebuilds the projection scope by applying tracked aggregate streams through the domain projection path.
    /// </summary>
    /// <param name="scope">The rebuild checkpoint scope.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task RebuildProjectionAsync(ProjectionRebuildCheckpointScope scope, CancellationToken cancellationToken = default);
}
