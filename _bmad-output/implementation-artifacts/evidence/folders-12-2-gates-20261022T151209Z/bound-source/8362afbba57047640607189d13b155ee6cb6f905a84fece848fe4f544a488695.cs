namespace Hexalith.EventStore.Server.Actors;

/// <summary>
/// Checkpoints pipeline stages in actor state for crash-recovery resume (NFR25).
/// All writes use SetStateAsync (staging). The caller commits atomically via SaveStateAsync.
/// </summary>
public interface IActorStateMachine {
    /// <summary>
    /// Stages a pipeline state checkpoint. Does NOT call SaveStateAsync.
    /// </summary>
    /// <param name="pipelineKeyPrefix">The pipeline key prefix from AggregateIdentity.</param>
    /// <param name="state">The pipeline state to checkpoint.</param>
    Task CheckpointAsync(string pipelineKeyPrefix, PipelineState state);

    /// <summary>
    /// Loads an existing pipeline state for the given correlation ID (resume detection).
    /// </summary>
    /// <param name="pipelineKeyPrefix">The pipeline key prefix from AggregateIdentity.</param>
    /// <param name="correlationId">The correlation identifier to look up.</param>
    /// <returns>The pipeline state if found; otherwise, <c>null</c>.</returns>
    Task<PipelineState?> LoadPipelineStateAsync(string pipelineKeyPrefix, string correlationId);

    /// <summary>
    /// Stages removal of pipeline state key. Does NOT call SaveStateAsync.
    /// </summary>
    /// <param name="pipelineKeyPrefix">The pipeline key prefix from AggregateIdentity.</param>
    /// <param name="correlationId">The correlation identifier to clean up.</param>
    Task CleanupPipelineAsync(string pipelineKeyPrefix, string correlationId);
}
