using Hexalith.EventStore.Contracts.Streams;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>
/// Result of persisting projection rebuild checkpoint progress.
/// </summary>
/// <param name="Succeeded">Whether the checkpoint save succeeded or was already covered.</param>
/// <param name="ReasonCode">Optional stable failure reason code.</param>
/// <param name="Checkpoint">The saved or already-covered checkpoint.</param>
public sealed record ProjectionRebuildCheckpointSaveResult(
    bool Succeeded,
    string? ReasonCode,
    ProjectionRebuildCheckpoint? Checkpoint) {
    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static ProjectionRebuildCheckpointSaveResult Success(ProjectionRebuildCheckpoint checkpoint)
        => new(true, null, checkpoint);

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    public static ProjectionRebuildCheckpointSaveResult Failure(string reasonCode)
        => new(false, reasonCode, null);
}
