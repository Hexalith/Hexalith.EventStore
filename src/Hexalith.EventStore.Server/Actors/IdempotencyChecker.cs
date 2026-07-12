using Dapr.Actors.Runtime;

using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>
/// Checks and records command idempotency using DAPR actor state.
/// Created per actor call because it requires the actor's state manager.
/// </summary>
public partial class IdempotencyChecker(
    IActorStateManager stateManager,
    ILogger<IdempotencyChecker> logger,
    TimeProvider? timeProvider = null) : IIdempotencyChecker
{
    private const string KeyPrefix = "idempotency:";

    private TimeProvider TimeProvider { get; } = timeProvider ?? TimeProvider.System;

    /// <inheritdoc/>
    public async Task<IdempotencyCheckResult> CheckAsync(CommandProcessingIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        identity.Validate();

        string messageKey = GetKey(identity.MessageId);
        ConditionalValue<IdempotencyRecord> messageResult = await stateManager
            .TryGetStateAsync<IdempotencyRecord>(messageKey)
            .ConfigureAwait(false);

        if (messageResult.HasValue)
        {
            return await ClassifyAsync(identity, messageKey, messageResult.Value, isLegacyLookup: false)
                .ConfigureAwait(false);
        }

        if (string.Equals(identity.MessageId, identity.CausationId, StringComparison.Ordinal))
        {
            Log.IdempotencyCacheMiss(logger, identity.MessageId);
            return new IdempotencyCheckResult(IdempotencyCheckOutcome.Miss);
        }

        string legacyKey = GetKey(identity.CausationId);
        ConditionalValue<IdempotencyRecord> legacyResult = await stateManager
            .TryGetStateAsync<IdempotencyRecord>(legacyKey)
            .ConfigureAwait(false);
        if (!legacyResult.HasValue)
        {
            Log.IdempotencyCacheMiss(logger, identity.MessageId);
            return new IdempotencyCheckResult(IdempotencyCheckOutcome.Miss);
        }

        return await ClassifyAsync(identity, legacyKey, legacyResult.Value, isLegacyLookup: true)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task RecordAsync(
        CommandProcessingIdentity identity,
        CommandProcessingResult result,
        DateTimeOffset expiresAt,
        IdempotencyRecordDisposition disposition)
    {
        ArgumentNullException.ThrowIfNull(identity);
        identity.Validate();
        ArgumentNullException.ThrowIfNull(result);

        DateTimeOffset processedAt = TimeProvider.GetUtcNow();
        IdempotencyRecord record = IdempotencyRecord.FromResult(
            identity,
            result,
            processedAt,
            expiresAt,
            disposition);

        await stateManager
            .SetStateAsync(GetKey(identity.MessageId), record)
            .ConfigureAwait(false);

        Log.IdempotencyRecordStored(logger, identity.MessageId);
    }

    private async Task<IdempotencyCheckResult> ClassifyAsync(
        CommandProcessingIdentity identity,
        string sourceKey,
        IdempotencyRecord record,
        bool isLegacyLookup)
    {
        if (!identity.Matches(record.MessageId, record.CausationId, record.CommandType)
            || record.Disposition is null
            || record.ExpiresAt is null)
        {
            Log.IdempotencyIdentityConflict(logger, identity.MessageId);
            return new IdempotencyCheckResult(IdempotencyCheckOutcome.IdentityConflict);
        }

        if (record.ExpiresAt <= TimeProvider.GetUtcNow())
        {
            _ = await stateManager.TryRemoveStateAsync(sourceKey).ConfigureAwait(false);
            Log.IdempotencyRecordExpired(logger, identity.MessageId);
            return new IdempotencyCheckResult(
                IdempotencyCheckOutcome.Expired,
                StateMutationStaged: true);
        }

        if (isLegacyLookup)
        {
            await stateManager.SetStateAsync(GetKey(identity.MessageId), record).ConfigureAwait(false);
            _ = await stateManager.TryRemoveStateAsync(sourceKey).ConfigureAwait(false);
            Log.IdempotencyLegacyMigrated(logger, identity.MessageId);
            return new IdempotencyCheckResult(
                IdempotencyCheckOutcome.LegacyMigration,
                record.ToResult(),
                StateMutationStaged: true);
        }

        Log.IdempotencyCacheHit(logger, identity.MessageId);
        return record.Disposition == IdempotencyRecordDisposition.Recoverable
            ? new IdempotencyCheckResult(IdempotencyCheckOutcome.RetryableRecoverable, record.ToResult())
            : new IdempotencyCheckResult(IdempotencyCheckOutcome.ExactTerminalDuplicate, record.ToResult());
    }

    private static string GetKey(string messageId) => $"{KeyPrefix}{messageId}";

    private static partial class Log
    {
        [LoggerMessage(
            EventId = 5000,
            Level = LogLevel.Debug,
            Message = "Idempotency cache hit: MessageId={MessageId}, Stage=IdempotencyCacheHit")]
        public static partial void IdempotencyCacheHit(ILogger logger, string messageId);

        [LoggerMessage(
            EventId = 5001,
            Level = LogLevel.Debug,
            Message = "Idempotency cache miss: MessageId={MessageId}, Stage=IdempotencyCacheMiss")]
        public static partial void IdempotencyCacheMiss(ILogger logger, string messageId);

        [LoggerMessage(
            EventId = 5002,
            Level = LogLevel.Debug,
            Message = "Idempotency record stored: MessageId={MessageId}, Stage=IdempotencyRecordStored")]
        public static partial void IdempotencyRecordStored(ILogger logger, string messageId);

        [LoggerMessage(
            EventId = 5003,
            Level = LogLevel.Warning,
            Message = "Idempotency identity conflict: MessageId={MessageId}, Stage=IdempotencyIdentityConflict")]
        public static partial void IdempotencyIdentityConflict(ILogger logger, string messageId);

        [LoggerMessage(
            EventId = 5004,
            Level = LogLevel.Debug,
            Message = "Idempotency record expired: MessageId={MessageId}, Stage=IdempotencyRecordExpired")]
        public static partial void IdempotencyRecordExpired(ILogger logger, string messageId);

        [LoggerMessage(
            EventId = 5005,
            Level = LogLevel.Information,
            Message = "Idempotency legacy record migrated: MessageId={MessageId}, Stage=IdempotencyLegacyMigrated")]
        public static partial void IdempotencyLegacyMigrated(ILogger logger, string messageId);
    }
}
