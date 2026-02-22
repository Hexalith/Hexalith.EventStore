
using Dapr.Actors.Runtime;

using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.Server.Actors;
/// <summary>
/// Checkpoints pipeline stages in actor state for crash-recovery resume (NFR25).
/// Created per-actor-call (same pattern as IdempotencyChecker, EventPersister).
/// All writes use SetStateAsync (staging) -- the caller (AggregateActor) commits atomically
/// with SaveStateAsync to ensure checkpoint + events are atomic (AC #9).
/// </summary>
public class ActorStateMachine(
    IActorStateManager stateManager,
    ILogger<ActorStateMachine> logger) : IActorStateMachine {
    /// <inheritdoc/>
    public async Task CheckpointAsync(string pipelineKeyPrefix, PipelineState state) {
        ArgumentException.ThrowIfNullOrWhiteSpace(pipelineKeyPrefix);
        ArgumentNullException.ThrowIfNull(state);

        string key = $"{pipelineKeyPrefix}{state.CorrelationId}";

        await stateManager
            .SetStateAsync(key, state)
            .ConfigureAwait(false);

        logger.LogDebug(
            "Pipeline checkpoint staged: Key={Key}, Stage={Stage}, CorrelationId={CorrelationId}",
            key,
            state.CurrentStage,
            state.CorrelationId);
    }

    /// <inheritdoc/>
    public async Task<PipelineState?> LoadPipelineStateAsync(string pipelineKeyPrefix, string correlationId) {
        ArgumentException.ThrowIfNullOrWhiteSpace(pipelineKeyPrefix);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        string key = $"{pipelineKeyPrefix}{correlationId}";

        ConditionalValue<PipelineState> result = await stateManager
            .TryGetStateAsync<PipelineState>(key)
            .ConfigureAwait(false);

        if (result.HasValue) {
            logger.LogDebug(
                "Pipeline state found: Key={Key}, Stage={Stage}, CorrelationId={CorrelationId}",
                key,
                result.Value.CurrentStage,
                correlationId);
            return result.Value;
        }

        logger.LogDebug("No pipeline state found: Key={Key}, CorrelationId={CorrelationId}", key, correlationId);
        return null;
    }

    /// <inheritdoc/>
    public async Task CleanupPipelineAsync(string pipelineKeyPrefix, string correlationId) {
        ArgumentException.ThrowIfNullOrWhiteSpace(pipelineKeyPrefix);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        string key = $"{pipelineKeyPrefix}{correlationId}";

        _ = await stateManager
            .TryRemoveStateAsync(key)
            .ConfigureAwait(false);

        logger.LogDebug(
            "Pipeline state cleanup staged: Key={Key}, CorrelationId={CorrelationId}",
            key,
            correlationId);
    }
}
