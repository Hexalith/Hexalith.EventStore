using System.Security.Cryptography;
using System.Text;

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
    private const string LegacyRedirectPrefix = "idempotency-legacy-redirect:";

    private TimeProvider TimeProvider { get; } = timeProvider ?? TimeProvider.System;

    /// <inheritdoc/>
    public async Task<IdempotencyCheckResult> CheckAsync(CommandProcessingIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        identity.Validate();

        ConditionalValue<IdempotencyLegacySourceRedirectRecord> redirect = await stateManager
            .TryGetStateAsync<IdempotencyLegacySourceRedirectRecord>(GetLegacyRedirectKey(identity.MessageId))
            .ConfigureAwait(false);
        if (redirect.HasValue)
        {
            return IsValidRedirect(redirect.Value)
                ? new IdempotencyCheckResult(IdempotencyCheckOutcome.RedirectedLegacy)
                : new IdempotencyCheckResult(IdempotencyCheckOutcome.IdentityConflict);
        }

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
            Log.IdempotencyCacheMiss(logger);
            return new IdempotencyCheckResult(IdempotencyCheckOutcome.Miss);
        }

        string legacyKey = GetKey(identity.CausationId);
        ConditionalValue<IdempotencyRecord> legacyResult = await stateManager
            .TryGetStateAsync<IdempotencyRecord>(legacyKey)
            .ConfigureAwait(false);
        if (!legacyResult.HasValue)
        {
            Log.IdempotencyCacheMiss(logger);
            return new IdempotencyCheckResult(IdempotencyCheckOutcome.Miss);
        }

        return await ClassifyAsync(identity, legacyKey, legacyResult.Value, isLegacyLookup: true)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IdempotencyCheckResult> InspectAsync(CommandProcessingIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        identity.Validate();
        ConditionalValue<IdempotencyLegacySourceRedirectRecord> redirect = await stateManager
            .TryGetStateAsync<IdempotencyLegacySourceRedirectRecord>(GetLegacyRedirectKey(identity.MessageId))
            .ConfigureAwait(false);
        if (redirect.HasValue)
        {
            return IsValidRedirect(redirect.Value)
                ? new IdempotencyCheckResult(IdempotencyCheckOutcome.RedirectedLegacy)
                : new IdempotencyCheckResult(IdempotencyCheckOutcome.IdentityConflict);
        }

        ConditionalValue<IdempotencyRecord> stored = await stateManager
            .TryGetStateAsync<IdempotencyRecord>(GetKey(identity.MessageId))
            .ConfigureAwait(false);
        if (!stored.HasValue)
        {
            return new IdempotencyCheckResult(IdempotencyCheckOutcome.Miss);
        }

        IdempotencyRecord record = stored.Value;
        if (!identity.Matches(record.MessageId, record.CausationId, record.CommandType)
            || record.Disposition is null
            || record.ExpiresAt is null)
        {
            return new IdempotencyCheckResult(IdempotencyCheckOutcome.IdentityConflict);
        }

        if (IsExpired(record))
        {
            return new IdempotencyCheckResult(IdempotencyCheckOutcome.Expired);
        }

        return record.Disposition == IdempotencyRecordDisposition.Recoverable
            ? new IdempotencyCheckResult(IdempotencyCheckOutcome.RetryableRecoverable, record.ToResult())
            : new IdempotencyCheckResult(IdempotencyCheckOutcome.ExactTerminalDuplicate, record.ToResult());
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

        Log.IdempotencyRecordStored(logger);
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
            Log.IdempotencyIdentityConflict(logger);
            return new IdempotencyCheckResult(IdempotencyCheckOutcome.IdentityConflict);
        }

        if (IsExpired(record))
        {
            Log.IdempotencyRecordExpired(logger);
            return new IdempotencyCheckResult(IdempotencyCheckOutcome.Expired);
        }

        if (isLegacyLookup)
        {
            await stateManager.SetStateAsync(GetKey(identity.MessageId), record).ConfigureAwait(false);
            _ = await stateManager.TryRemoveStateAsync(sourceKey).ConfigureAwait(false);
            Log.IdempotencyLegacyMigrated(logger);
            return new IdempotencyCheckResult(
                IdempotencyCheckOutcome.LegacyMigration,
                record.ToResult(),
                StateMutationStaged: true);
        }

        Log.IdempotencyCacheHit(logger);
        return record.Disposition == IdempotencyRecordDisposition.Recoverable
            ? new IdempotencyCheckResult(IdempotencyCheckOutcome.RetryableRecoverable, record.ToResult())
            : new IdempotencyCheckResult(IdempotencyCheckOutcome.ExactTerminalDuplicate, record.ToResult());
    }

    /// <inheritdoc/>
    public async Task<bool> TryCompleteRecoverableAsync(string messageId, DateTimeOffset expiresAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);

        string key = GetKey(messageId);
        ConditionalValue<IdempotencyRecord> stored = await stateManager
            .TryGetStateAsync<IdempotencyRecord>(key)
            .ConfigureAwait(false);

        if (!stored.HasValue
            || stored.Value is null
            || stored.Value.Disposition != IdempotencyRecordDisposition.Recoverable)
        {
            return false;
        }

        await stateManager
            .SetStateAsync(
                key,
                stored.Value with
                {
                    Disposition = IdempotencyRecordDisposition.Terminal,
                    ExpiresAt = expiresAt,
                })
            .ConfigureAwait(false);

        Log.IdempotencyRecoverableCompleted(logger);
        return true;
    }

    /// <summary>Inspects only the exact supported message-keyed source without mutation.</summary>
    internal async Task<IdempotencyLegacySourceInspection> InspectLegacySourceAsync(
        IdempotencyLegacySourceRequest request)
    {
        ValidateSourceRequest(request);
        ConditionalValue<IdempotencyLegacySourceRedirectRecord> redirect;
        ConditionalValue<IdempotencyRecord> stored;
        try
        {
            redirect = await stateManager
                .TryGetStateAsync<IdempotencyLegacySourceRedirectRecord>(
                    GetLegacyRedirectKey(request.ExecutionMessageId))
                .ConfigureAwait(false);
            stored = redirect.HasValue
                ? default
                : await stateManager
                    .TryGetStateAsync<IdempotencyRecord>(GetKey(request.ExecutionMessageId))
                    .ConfigureAwait(false);
        }
        catch (Exception)
        {
            return new IdempotencyLegacySourceInspection(IdempotencyLegacySourceDecision.Unavailable);
        }

        if (redirect.HasValue)
        {
            return MatchesRedirect(redirect.Value, request)
                ? new IdempotencyLegacySourceInspection(
                    IdempotencyLegacySourceDecision.Redirected,
                    IdempotencyLegacySourceEvidence.Compute(redirect.Value))
                : new IdempotencyLegacySourceInspection(IdempotencyLegacySourceDecision.Conflict);
        }

        if (!stored.HasValue)
        {
            return new IdempotencyLegacySourceInspection(IdempotencyLegacySourceDecision.Missing);
        }

        IdempotencyRecord record = stored.Value;
        if (!IsSupportedLegacySource(record))
        {
            return new IdempotencyLegacySourceInspection(IdempotencyLegacySourceDecision.Unsupported);
        }

        if (!string.Equals(record.MessageId, request.ExecutionMessageId, StringComparison.Ordinal)
            || !string.Equals(record.CorrelationId, request.ExecutionCorrelationId, StringComparison.Ordinal)
            || record.ProcessedAt != request.FirstConsumedAt
            || record.ExpiresAt != request.ReplayExpiresAt
            || !Equals(record.ToResult(), request.ReplayResult)
            || !FixedTimeEquals(
                IdempotencyLegacySourceEvidence.Compute(record),
                request.SourceEvidenceDigest))
        {
            return new IdempotencyLegacySourceInspection(IdempotencyLegacySourceDecision.Conflict);
        }

        return new IdempotencyLegacySourceInspection(
            record.ExpiresAt <= TimeProvider.GetUtcNow()
                ? IdempotencyLegacySourceDecision.Expired
                : IdempotencyLegacySourceDecision.Exact);
    }

    /// <summary>Persists the irreversible payload-free redirect after exact source proof.</summary>
    internal async Task<IdempotencyLegacySourceInspection> SetLegacySourceRedirectAsync(
        IdempotencyLegacySourceRedirectRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.TargetAdmissionActorId);
        ValidateSourceRequest(request.Source);
        ConditionalValue<IdempotencyLegacySourceRedirectRecord> existing;
        try
        {
            existing = await stateManager
                .TryGetStateAsync<IdempotencyLegacySourceRedirectRecord>(
                    GetLegacyRedirectKey(request.Source.ExecutionMessageId))
                .ConfigureAwait(false);
        }
        catch (Exception)
        {
            return new IdempotencyLegacySourceInspection(IdempotencyLegacySourceDecision.Unavailable);
        }
        if (existing.HasValue)
        {
            return MatchesRedirect(existing.Value, request.Source)
                && string.Equals(
                    existing.Value.TargetAdmissionActorId,
                    request.TargetAdmissionActorId,
                    StringComparison.Ordinal)
                ? new IdempotencyLegacySourceInspection(
                    IdempotencyLegacySourceDecision.Redirected,
                    IdempotencyLegacySourceEvidence.Compute(existing.Value))
                : new IdempotencyLegacySourceInspection(IdempotencyLegacySourceDecision.Conflict);
        }

        IdempotencyLegacySourceInspection inspection = await InspectLegacySourceAsync(request.Source)
            .ConfigureAwait(false);
        if (inspection.Decision is not (IdempotencyLegacySourceDecision.Exact
            or IdempotencyLegacySourceDecision.Expired))
        {
            return inspection;
        }

        var redirect = new IdempotencyLegacySourceRedirectRecord(
            IdempotencyLegacySourceRedirectRecord.CurrentSchemaVersion,
            request.Source.TenantPartition,
            request.Source.InventoryId,
            request.Source.MigrationId,
            request.Source.SourceEvidenceDigest,
            request.TargetAdmissionActorId);
        try
        {
            await stateManager.SetStateAsync(
                GetLegacyRedirectKey(request.Source.ExecutionMessageId),
                redirect).ConfigureAwait(false);
            await stateManager.SaveStateAsync().ConfigureAwait(false);
        }
        catch (Exception)
        {
            return new IdempotencyLegacySourceInspection(IdempotencyLegacySourceDecision.Unavailable);
        }
        return new IdempotencyLegacySourceInspection(
            IdempotencyLegacySourceDecision.Redirected,
            IdempotencyLegacySourceEvidence.Compute(redirect));
    }

    /// <summary>
    /// Story 4.4: a <see cref="IdempotencyRecordDisposition.Recoverable"/> record describes events
    /// that are committed but not yet published, so it must survive normal retention until the
    /// drain completes. <see cref="TryCompleteRecoverableAsync"/> transitions it to
    /// <see cref="IdempotencyRecordDisposition.Terminal"/> on drain success or drain exhaustion, at
    /// which point normal retention resumes -- without that transition the record would be
    /// immortal and every later retry would return <c>RetryableRecoverable</c> forever.
    /// </summary>
    private bool IsExpired(IdempotencyRecord record)
        => record.Disposition != IdempotencyRecordDisposition.Recoverable
            && record.ExpiresAt <= TimeProvider.GetUtcNow();

    private static string GetKey(string messageId) => $"{KeyPrefix}{messageId}";

    /// <summary>Builds the exact payload-free redirect state name for a message.</summary>
    internal static string GetLegacyRedirectKey(string messageId)
        => string.Concat(LegacyRedirectPrefix, messageId);

    private static bool IsSupportedLegacySource(IdempotencyRecord record)
        => !string.IsNullOrWhiteSpace(record.CausationId)
            && !string.IsNullOrWhiteSpace(record.CorrelationId)
            && !string.IsNullOrWhiteSpace(record.MessageId)
            && !string.IsNullOrWhiteSpace(record.CommandType)
            && record.ExpiresAt is not null
            && record.Disposition == IdempotencyRecordDisposition.Terminal;

    private static bool IsValidRedirect(IdempotencyLegacySourceRedirectRecord redirect)
        => redirect.SchemaVersion == IdempotencyLegacySourceRedirectRecord.CurrentSchemaVersion
            && !string.IsNullOrWhiteSpace(redirect.TenantPartition)
            && !string.IsNullOrWhiteSpace(redirect.InventoryId)
            && !string.IsNullOrWhiteSpace(redirect.MigrationId)
            && !string.IsNullOrWhiteSpace(redirect.SourceEvidenceDigest)
            && !string.IsNullOrWhiteSpace(redirect.TargetAdmissionActorId);

    private static bool MatchesRedirect(
        IdempotencyLegacySourceRedirectRecord redirect,
        IdempotencyLegacySourceRequest request)
        => IsValidRedirect(redirect)
            && string.Equals(redirect.TenantPartition, request.TenantPartition, StringComparison.Ordinal)
            && string.Equals(redirect.InventoryId, request.InventoryId, StringComparison.Ordinal)
            && string.Equals(redirect.MigrationId, request.MigrationId, StringComparison.Ordinal)
            && FixedTimeEquals(redirect.SourceEvidenceDigest, request.SourceEvidenceDigest);

    private static void ValidateSourceRequest(IdempotencyLegacySourceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.SchemaVersion != IdempotencyLegacySourceRequest.CurrentSchemaVersion
            || request.LegacySchemaVersion != 1
            || string.IsNullOrWhiteSpace(request.TenantPartition)
            || string.IsNullOrWhiteSpace(request.InventoryId)
            || string.IsNullOrWhiteSpace(request.MigrationId)
            || string.IsNullOrWhiteSpace(request.SourceEvidenceDigest)
            || string.IsNullOrWhiteSpace(request.ExecutionMessageId)
            || string.IsNullOrWhiteSpace(request.ExecutionCorrelationId)
            || request.ReplayExpiresAt < request.FirstConsumedAt
            || request.ReplayResult is null)
        {
            throw new InvalidOperationException("Legacy source inspection request is invalid.");
        }
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        byte[] leftBytes = Encoding.UTF8.GetBytes(left);
        byte[] rightBytes = Encoding.UTF8.GetBytes(right);
        try
        {
            return leftBytes.Length == rightBytes.Length
                && CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(leftBytes);
            CryptographicOperations.ZeroMemory(rightBytes);
        }
    }

    private static partial class Log
    {
        [LoggerMessage(
            EventId = 5000,
            Level = LogLevel.Debug,
            Message = "Idempotency cache hit. Stage=IdempotencyCacheHit")]
        public static partial void IdempotencyCacheHit(ILogger logger);

        [LoggerMessage(
            EventId = 5001,
            Level = LogLevel.Debug,
            Message = "Idempotency cache miss. Stage=IdempotencyCacheMiss")]
        public static partial void IdempotencyCacheMiss(ILogger logger);

        [LoggerMessage(
            EventId = 5002,
            Level = LogLevel.Debug,
            Message = "Idempotency record stored. Stage=IdempotencyRecordStored")]
        public static partial void IdempotencyRecordStored(ILogger logger);

        [LoggerMessage(
            EventId = 5003,
            Level = LogLevel.Warning,
            Message = "Idempotency identity conflict. Stage=IdempotencyIdentityConflict")]
        public static partial void IdempotencyIdentityConflict(ILogger logger);

        [LoggerMessage(
            EventId = 5004,
            Level = LogLevel.Debug,
            Message = "Idempotency record expired. Stage=IdempotencyRecordExpired")]
        public static partial void IdempotencyRecordExpired(ILogger logger);

        [LoggerMessage(
            EventId = 5005,
            Level = LogLevel.Information,
            Message = "Idempotency legacy record migrated. Stage=IdempotencyLegacyMigrated")]
        public static partial void IdempotencyLegacyMigrated(ILogger logger);

        [LoggerMessage(
            EventId = 5006,
            Level = LogLevel.Information,
            Message = "Idempotency recoverable record completed. Stage=IdempotencyRecoverableCompleted")]
        public static partial void IdempotencyRecoverableCompleted(ILogger logger);
    }
}
