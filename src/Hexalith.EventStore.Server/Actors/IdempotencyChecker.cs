namespace Hexalith.EventStore.Server.Actors;

using Dapr.Actors.Runtime;

using Microsoft.Extensions.Logging;

/// <summary>
/// Checks and records command idempotency using DAPR actor state.
/// Created per-actor-call (not via DI) because it requires the actor's IActorStateManager instance.
/// </summary>
public partial class IdempotencyChecker(
    IActorStateManager stateManager,
    ILogger<IdempotencyChecker> logger) : IIdempotencyChecker {
    private const string KeyPrefix = "idempotency:";

    /// <inheritdoc/>
    public async Task<CommandProcessingResult?> CheckAsync(string causationId) {
        ArgumentException.ThrowIfNullOrWhiteSpace(causationId);

        string key = $"{KeyPrefix}{causationId}";
        Dapr.Actors.Runtime.ConditionalValue<IdempotencyRecord> result = await stateManager
            .TryGetStateAsync<IdempotencyRecord>(key)
            .ConfigureAwait(false);

        if (result.HasValue) {
            Log.IdempotencyCacheHit(logger, causationId);
            return result.Value.ToResult();
        }

        Log.IdempotencyCacheMiss(logger, causationId);
        return null;
    }

    /// <inheritdoc/>
    public async Task RecordAsync(string causationId, CommandProcessingResult result) {
        ArgumentException.ThrowIfNullOrWhiteSpace(causationId);
        ArgumentNullException.ThrowIfNull(result);

        string key = $"{KeyPrefix}{causationId}";
        IdempotencyRecord record = IdempotencyRecord.FromResult(causationId, result);

        await stateManager
            .SetStateAsync(key, record)
            .ConfigureAwait(false);

        Log.IdempotencyRecordStored(logger, causationId);
    }

    private static partial class Log {
        [LoggerMessage(
            EventId = 5000,
            Level = LogLevel.Debug,
            Message = "Idempotency cache hit: CausationId={CausationId}, Stage=IdempotencyCacheHit")]
        public static partial void IdempotencyCacheHit(ILogger logger, string causationId);

        [LoggerMessage(
            EventId = 5001,
            Level = LogLevel.Debug,
            Message = "Idempotency cache miss: CausationId={CausationId}, Stage=IdempotencyCacheMiss")]
        public static partial void IdempotencyCacheMiss(ILogger logger, string causationId);

        [LoggerMessage(
            EventId = 5002,
            Level = LogLevel.Debug,
            Message = "Idempotency record stored: CausationId={CausationId}, Stage=IdempotencyRecordStored")]
        public static partial void IdempotencyRecordStored(ILogger logger, string causationId);
    }
}
