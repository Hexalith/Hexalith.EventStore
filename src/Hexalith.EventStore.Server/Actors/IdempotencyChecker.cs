namespace Hexalith.EventStore.Server.Actors;

using Dapr.Actors.Runtime;

using Microsoft.Extensions.Logging;

/// <summary>
/// Checks and records command idempotency using DAPR actor state.
/// Created per-actor-call (not via DI) because it requires the actor's IActorStateManager instance.
/// </summary>
public class IdempotencyChecker(
    IActorStateManager stateManager,
    ILogger<IdempotencyChecker> logger) : IIdempotencyChecker
{
    private const string KeyPrefix = "idempotency:";

    /// <inheritdoc/>
    public async Task<CommandProcessingResult?> CheckAsync(string causationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(causationId);

        string key = $"{KeyPrefix}{causationId}";
        Dapr.Actors.Runtime.ConditionalValue<IdempotencyRecord> result = await stateManager
            .TryGetStateAsync<IdempotencyRecord>(key)
            .ConfigureAwait(false);

        if (result.HasValue)
        {
            logger.LogDebug("Idempotency cache hit: CausationId={CausationId}", causationId);
            return result.Value.ToResult();
        }

        logger.LogDebug("Idempotency cache miss: CausationId={CausationId}", causationId);
        return null;
    }

    /// <inheritdoc/>
    public async Task RecordAsync(string causationId, CommandProcessingResult result)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(causationId);
        ArgumentNullException.ThrowIfNull(result);

        string key = $"{KeyPrefix}{causationId}";
        IdempotencyRecord record = IdempotencyRecord.FromResult(causationId, result);

        await stateManager
            .SetStateAsync(key, record)
            .ConfigureAwait(false);

        logger.LogDebug("Idempotency record stored: CausationId={CausationId}", causationId);
    }
}
