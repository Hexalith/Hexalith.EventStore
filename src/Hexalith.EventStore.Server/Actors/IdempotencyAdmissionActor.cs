using System.Security.Cryptography;
using System.Text;

using Dapr.Actors.Runtime;

using Hexalith.EventStore.Contracts.Commands;

using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Owns durable tenant/key admission, fencing, replay, and minimal expiry compaction.</summary>
public sealed partial class IdempotencyAdmissionActor(
    ActorHost host,
    ILogger<IdempotencyAdmissionActor> logger,
    TimeProvider? timeProvider = null)
    : Actor(host), IIdempotencyAdmissionActor
{
    /// <summary>Gets the Dapr actor type name.</summary>
    public const string ActorTypeName = nameof(IdempotencyAdmissionActor);

    /// <summary>Gets the fixed actor-state entry name.</summary>
    public const string StateName = "admission";

    /// <summary>Gets the approved metadata-only consumed-key tombstone entry name.</summary>
    public const string TombstoneStateName = "tombstone";

    /// <summary>Gets the protected promotion marker state name.</summary>
    public const string PromotionStateName = "promotion";

    /// <summary>Gets the protected promotion redirect state name.</summary>
    public const string RedirectStateName = "redirect";

    private TimeProvider Clock { get; } = timeProvider ?? TimeProvider.System;

    /// <inheritdoc/>
    public async Task<IdempotencyAdmissionInspection> InspectAsync()
    {
        ConditionalValue<IdempotencyAdmissionRedirectRecord> redirect = await StateManager
            .TryGetStateAsync<IdempotencyAdmissionRedirectRecord>(RedirectStateName)
            .ConfigureAwait(false);
        if (redirect.HasValue
            && (redirect.Value.SchemaVersion != IdempotencyAdmissionRedirectRecord.CurrentSchemaVersion
                || string.IsNullOrWhiteSpace(redirect.Value.TargetActorId)))
        {
            throw new InvalidOperationException("The idempotency admission redirect is corrupt.");
        }

        ConditionalValue<IdempotencyAdmissionRecord> stored = await StateManager
            .TryGetStateAsync<IdempotencyAdmissionRecord>(StateName)
            .ConfigureAwait(false);
        ConditionalValue<IdempotencyAdmissionTombstone> tombstone = await StateManager
            .TryGetStateAsync<IdempotencyAdmissionTombstone>(TombstoneStateName)
            .ConfigureAwait(false);
        if (stored.HasValue && tombstone.HasValue)
        {
            throw new InvalidOperationException("The idempotency admission contains both live and compacted state.");
        }

        if ((stored.HasValue && !IsStructurallyValid(stored.Value))
            || (tombstone.HasValue && !IsStructurallyValid(tombstone.Value)))
        {
            throw new InvalidOperationException("The idempotency admission state is corrupt.");
        }

        return new IdempotencyAdmissionInspection(
            stored.HasValue || tombstone.HasValue,
            stored.HasValue ? stored.Value : null,
            redirect.HasValue ? redirect.Value.TargetActorId : null,
            tombstone.HasValue ? tombstone.Value : null);
    }

    /// <inheritdoc/>
    public async Task<IdempotencyAdmissionResult> AdmitAsync(IdempotencyAdmissionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!IsValidRequest(request))
        {
            Log.CorruptState(logger);
            return new IdempotencyAdmissionResult(IdempotencyAdmissionDecision.Corrupt);
        }

        ConditionalValue<IdempotencyAdmissionRedirectRecord> redirect = await StateManager
            .TryGetStateAsync<IdempotencyAdmissionRedirectRecord>(RedirectStateName)
            .ConfigureAwait(false);
        if (redirect.HasValue)
        {
            if (redirect.Value.SchemaVersion != IdempotencyAdmissionRedirectRecord.CurrentSchemaVersion
                || string.IsNullOrWhiteSpace(redirect.Value.TargetActorId))
            {
                return new IdempotencyAdmissionResult(IdempotencyAdmissionDecision.Corrupt);
            }

            return new IdempotencyAdmissionResult(
                IdempotencyAdmissionDecision.Redirect,
                RedirectActorId: redirect.Value.TargetActorId);
        }

        ConditionalValue<IdempotencyAdmissionPromotionRecord> promotion = await StateManager
            .TryGetStateAsync<IdempotencyAdmissionPromotionRecord>(PromotionStateName)
            .ConfigureAwait(false);
        if (promotion.HasValue)
        {
            if (promotion.Value.SchemaVersion != IdempotencyAdmissionPromotionRecord.CurrentSchemaVersion
                || string.IsNullOrWhiteSpace(promotion.Value.SourceActorId))
            {
                return new IdempotencyAdmissionResult(IdempotencyAdmissionDecision.Corrupt);
            }

            if (!promotion.Value.Activated)
            {
                return new IdempotencyAdmissionResult(IdempotencyAdmissionDecision.Pending);
            }
        }

        ConditionalValue<IdempotencyAdmissionTombstone> compacted = await StateManager
            .TryGetStateAsync<IdempotencyAdmissionTombstone>(TombstoneStateName)
            .ConfigureAwait(false);
        ConditionalValue<IdempotencyAdmissionRecord> stored = await StateManager
            .TryGetStateAsync<IdempotencyAdmissionRecord>(StateName)
            .ConfigureAwait(false);
        DateTimeOffset now = Clock.GetUtcNow();
        if (compacted.HasValue)
        {
            if (stored.HasValue || !IsVerifiable(compacted.Value, request))
            {
                Log.CorruptState(logger);
                return new IdempotencyAdmissionResult(IdempotencyAdmissionDecision.Corrupt);
            }

            if (!FixedTimeEquals(compacted.Value.VerificationTag, request.VerificationTag))
            {
                Log.Collision(logger);
                return new IdempotencyAdmissionResult(IdempotencyAdmissionDecision.Collision);
            }

            DateTimeOffset observedAt = Max(compacted.Value.LastObservedAt, now);
            if (observedAt > compacted.Value.LastObservedAt)
            {
                await PersistAsync(compacted.Value with { LastObservedAt = observedAt }).ConfigureAwait(false);
            }

            Log.Expired(logger);
            return new IdempotencyAdmissionResult(IdempotencyAdmissionDecision.Expired);
        }

        if (!stored.HasValue)
        {
            var reserved = new IdempotencyAdmissionRecord(
                IdempotencyAdmissionRecord.CurrentSchemaVersion,
                IdempotencyAdmissionState.Reserved,
                request.TenantPartition,
                request.DigestKeyVersion,
                request.KeyDigest,
                request.VerificationTag,
                request.IntentDigest,
                request.RetentionTier,
                now,
                now,
                ReplayExpiresAt: null,
                FencingToken: 1,
                ReplayResult: null,
                ExecutionMessageId: request.ExecutionMessageId,
                ExecutionCorrelationId: request.ExecutionCorrelationId);
            await PersistAsync(reserved).ConfigureAwait(false);
            Log.Reserved(logger);
            return new IdempotencyAdmissionResult(
                IdempotencyAdmissionDecision.Execute,
                reserved.FencingToken,
                ExecutionMessageId: reserved.ExecutionMessageId,
                ExecutionCorrelationId: reserved.ExecutionCorrelationId);
        }

        IdempotencyAdmissionRecord record = stored.Value;
        if (!IsVerifiable(record, request))
        {
            Log.CorruptState(logger);
            return new IdempotencyAdmissionResult(IdempotencyAdmissionDecision.Corrupt);
        }

        if (!FixedTimeEquals(record.VerificationTag, request.VerificationTag))
        {
            Log.Collision(logger);
            return new IdempotencyAdmissionResult(
                IdempotencyAdmissionDecision.Collision,
                record.FencingToken,
                ExecutionMessageId: record.ExecutionMessageId,
                ExecutionCorrelationId: record.ExecutionCorrelationId);
        }

        DateTimeOffset effectiveNow = now > record.LastObservedAt ? now : record.LastObservedAt;
        if (record.State == IdempotencyAdmissionState.Expired)
        {
            await CompactAsync(record, effectiveNow).ConfigureAwait(false);
            Log.Expired(logger);
            return new IdempotencyAdmissionResult(IdempotencyAdmissionDecision.Expired);
        }

        if (record.State == IdempotencyAdmissionState.Terminal)
        {
            if (record.ReplayExpiresAt is null || record.ReplayResult is null)
            {
                Log.CorruptState(logger);
                return new IdempotencyAdmissionResult(IdempotencyAdmissionDecision.Corrupt);
            }

            if (effectiveNow >= record.ReplayExpiresAt.Value)
            {
                await CompactAsync(record, effectiveNow).ConfigureAwait(false);
                Log.Expired(logger);
                return new IdempotencyAdmissionResult(IdempotencyAdmissionDecision.Expired);
            }
        }

        if (!FixedTimeEquals(record.IntentDigest, request.IntentDigest))
        {
            Log.Conflict(logger);
            return new IdempotencyAdmissionResult(
                IdempotencyAdmissionDecision.Conflict,
                record.FencingToken,
                ExecutionMessageId: record.ExecutionMessageId,
                ExecutionCorrelationId: record.ExecutionCorrelationId);
        }

        if (effectiveNow > record.LastObservedAt)
        {
            record = record with { LastObservedAt = effectiveNow };
            await PersistAsync(record).ConfigureAwait(false);
        }

        IdempotencyAdmissionDecision decision = record.State switch
        {
            IdempotencyAdmissionState.Reserved or IdempotencyAdmissionState.Pending
                => IdempotencyAdmissionDecision.Pending,
            IdempotencyAdmissionState.Recoverable => IdempotencyAdmissionDecision.Recoverable,
            IdempotencyAdmissionState.UnknownProviderOutcome
                => IdempotencyAdmissionDecision.UnknownProviderOutcome,
            IdempotencyAdmissionState.Terminal => IdempotencyAdmissionDecision.Replay,
            _ => IdempotencyAdmissionDecision.Corrupt,
        };

        Log.Classified(logger, decision.ToString());
        return new IdempotencyAdmissionResult(
            decision,
            record.FencingToken,
            decision == IdempotencyAdmissionDecision.Replay ? record.ReplayResult : null,
            ExecutionMessageId: record.ExecutionMessageId,
            ExecutionCorrelationId: record.ExecutionCorrelationId);
    }

    /// <inheritdoc/>
    public async Task BeginAsync(IdempotencyAdmissionTransitionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        IdempotencyAdmissionRecord record = await LoadRequiredAsync().ConfigureAwait(false);
        EnsureFence(record, request.FencingToken);
        if (record.State is not (IdempotencyAdmissionState.Reserved or IdempotencyAdmissionState.Recoverable))
        {
            throw new InvalidOperationException("The admission reservation cannot begin from its current state.");
        }

        DateTimeOffset now = Max(record.LastObservedAt, Clock.GetUtcNow());
        await PersistAsync(record with
        {
            State = IdempotencyAdmissionState.Pending,
            LastObservedAt = now,
        }).ConfigureAwait(false);
        Log.Began(logger);
    }

    /// <inheritdoc/>
    public async Task CompleteAsync(IdempotencyAdmissionCompletionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Result);
        IdempotencyAdmissionRecord record = await LoadRequiredAsync().ConfigureAwait(false);
        EnsureFence(record, request.FencingToken);
        if (record.State is not (IdempotencyAdmissionState.Pending
            or IdempotencyAdmissionState.Recoverable
            or IdempotencyAdmissionState.UnknownProviderOutcome))
        {
            throw new InvalidOperationException("The admission cannot be finalized from its current state.");
        }

        DateTimeOffset finalizedAt = Max(record.LastObservedAt, Clock.GetUtcNow());
        DateTimeOffset expiresAt = record.RetentionTier switch
        {
            IdempotencyReplayRetentionTier.Mutation => finalizedAt.AddSeconds(86_400),
            IdempotencyReplayRetentionTier.Commit => finalizedAt.AddYears(7),
            _ => throw new InvalidOperationException("The admission record contains an unknown retention tier."),
        };
        await PersistAsync(record with
        {
            State = IdempotencyAdmissionState.Terminal,
            LastObservedAt = finalizedAt,
            ReplayExpiresAt = expiresAt,
            ReplayResult = request.Result,
        }).ConfigureAwait(false);
        Log.Completed(logger);
    }

    /// <inheritdoc/>
    public async Task MarkRecoveryAsync(IdempotencyAdmissionRecoveryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.State is not (IdempotencyAdmissionState.Recoverable
            or IdempotencyAdmissionState.UnknownProviderOutcome))
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Recovery state must be recoverable or unknown provider outcome.");
        }

        IdempotencyAdmissionRecord record = await LoadRequiredAsync().ConfigureAwait(false);
        EnsureFence(record, request.FencingToken);
        if (record.State is not (IdempotencyAdmissionState.Reserved
            or IdempotencyAdmissionState.Pending
            or IdempotencyAdmissionState.Recoverable))
        {
            throw new InvalidOperationException("The admission cannot enter recovery from its current state.");
        }

        DateTimeOffset now = Max(record.LastObservedAt, Clock.GetUtcNow());
        await PersistAsync(record with
        {
            State = request.State,
            LastObservedAt = now,
        }).ConfigureAwait(false);
        Log.RecoveryMarked(logger, request.State.ToString());
    }

    /// <inheritdoc/>
    public async Task PreparePromotionAsync(IdempotencyAdmissionPromotionImportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SourceActorId);
        if ((request.Record is null) == (request.Tombstone is null))
        {
            throw new ArgumentException("Exactly one promotion state payload is required.", nameof(request));
        }


        if ((request.Record is not null && !IsStructurallyValid(request.Record))
            || (request.Tombstone is not null && !IsStructurallyValid(request.Tombstone)))
        {
            throw new InvalidOperationException("The imported idempotency promotion state is corrupt.");
        }

        ConditionalValue<IdempotencyAdmissionRecord> stored = await StateManager
            .TryGetStateAsync<IdempotencyAdmissionRecord>(StateName)
            .ConfigureAwait(false);
        ConditionalValue<IdempotencyAdmissionTombstone> tombstone = await StateManager
            .TryGetStateAsync<IdempotencyAdmissionTombstone>(TombstoneStateName)
            .ConfigureAwait(false);
        ConditionalValue<IdempotencyAdmissionPromotionRecord> promotion = await StateManager
            .TryGetStateAsync<IdempotencyAdmissionPromotionRecord>(PromotionStateName)
            .ConfigureAwait(false);
        if (stored.HasValue || tombstone.HasValue || promotion.HasValue)
        {
            bool samePayload = request.Record is not null
                ? stored.HasValue && !tombstone.HasValue && SameImportedRecord(stored.Value, request.Record)
                : tombstone.HasValue && !stored.HasValue && SameImportedTombstone(tombstone.Value, request.Tombstone!);
            if (!promotion.HasValue
                || promotion.Value.SchemaVersion != IdempotencyAdmissionPromotionRecord.CurrentSchemaVersion
                || !string.Equals(promotion.Value.SourceActorId, request.SourceActorId, StringComparison.Ordinal)
                || !samePayload)
            {
                throw new InvalidOperationException("The idempotency promotion target contains different state.");
            }

            return;
        }

        if (request.Record is not null)
        {
            await StateManager.SetStateAsync(StateName, request.Record).ConfigureAwait(false);
        }
        else
        {
            await StateManager.SetStateAsync(TombstoneStateName, request.Tombstone!).ConfigureAwait(false);
        }
        await StateManager.SetStateAsync(
            PromotionStateName,
            new IdempotencyAdmissionPromotionRecord(
                IdempotencyAdmissionPromotionRecord.CurrentSchemaVersion,
                request.SourceActorId,
                Activated: false)).ConfigureAwait(false);
        await StateManager.SaveStateAsync().ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task SetRedirectAsync(IdempotencyAdmissionRedirectRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.TargetActorId);
        await EnsureAdmissionExistsAsync().ConfigureAwait(false);
        ConditionalValue<IdempotencyAdmissionRedirectRecord> existing = await StateManager
            .TryGetStateAsync<IdempotencyAdmissionRedirectRecord>(RedirectStateName)
            .ConfigureAwait(false);
        if (existing.HasValue)
        {
            if (existing.Value.SchemaVersion != IdempotencyAdmissionRedirectRecord.CurrentSchemaVersion
                || !string.Equals(existing.Value.TargetActorId, request.TargetActorId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The idempotency admission source has a different redirect.");
            }

            return;
        }

        await StateManager.SetStateAsync(
            RedirectStateName,
            new IdempotencyAdmissionRedirectRecord(
                IdempotencyAdmissionRedirectRecord.CurrentSchemaVersion,
                request.TargetActorId)).ConfigureAwait(false);
        await StateManager.SaveStateAsync().ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task ActivatePromotionAsync(IdempotencyAdmissionPromotionActivationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SourceActorId);
        await EnsureAdmissionExistsAsync().ConfigureAwait(false);
        ConditionalValue<IdempotencyAdmissionPromotionRecord> promotion = await StateManager
            .TryGetStateAsync<IdempotencyAdmissionPromotionRecord>(PromotionStateName)
            .ConfigureAwait(false);
        if (!promotion.HasValue
            || promotion.Value.SchemaVersion != IdempotencyAdmissionPromotionRecord.CurrentSchemaVersion
            || !string.Equals(promotion.Value.SourceActorId, request.SourceActorId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The idempotency promotion target is not prepared for this source.");
        }

        if (promotion.Value.Activated)
        {
            return;
        }

        await StateManager.SetStateAsync(
            PromotionStateName,
            promotion.Value with { Activated = true }).ConfigureAwait(false);
        await StateManager.SaveStateAsync().ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<bool> PurgeTombstoneAsync(IdempotencyAdmissionPurgeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ConditionalValue<IdempotencyAdmissionRecord> live = await StateManager
            .TryGetStateAsync<IdempotencyAdmissionRecord>(StateName)
            .ConfigureAwait(false);
        ConditionalValue<IdempotencyAdmissionTombstone> tombstone = await StateManager
            .TryGetStateAsync<IdempotencyAdmissionTombstone>(TombstoneStateName)
            .ConfigureAwait(false);
        if (live.HasValue)
        {
            return false;
        }

        if (!tombstone.HasValue)
        {
            return true;
        }

        if (!string.Equals(tombstone.Value.TenantPartition, request.TenantPartition, StringComparison.Ordinal)
            || !string.Equals(tombstone.Value.DigestKeyVersion, request.DigestKeyVersion, StringComparison.Ordinal)
            || !string.Equals(tombstone.Value.KeyDigest, request.KeyDigest, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The governed tombstone purge identity is invalid.");
        }

        _ = await StateManager.TryRemoveStateAsync(TombstoneStateName).ConfigureAwait(false);
        _ = await StateManager.TryRemoveStateAsync(RedirectStateName).ConfigureAwait(false);
        _ = await StateManager.TryRemoveStateAsync(PromotionStateName).ConfigureAwait(false);
        await StateManager.SaveStateAsync().ConfigureAwait(false);
        return true;
    }

    private static void EnsureFence(IdempotencyAdmissionRecord record, long fencingToken)
    {
        if (fencingToken <= 0 || record.FencingToken != fencingToken)
        {
            throw new InvalidOperationException("The idempotency fencing token is stale or invalid.");
        }
    }

    private static bool IsValidRequest(IdempotencyAdmissionRequest request)
        => request.SchemaVersion == IdempotencyAdmissionRecord.CurrentSchemaVersion
            && !string.IsNullOrWhiteSpace(request.TenantPartition)
            && !string.IsNullOrWhiteSpace(request.DigestKeyVersion)
            && !string.IsNullOrWhiteSpace(request.KeyDigest)
            && !string.IsNullOrWhiteSpace(request.VerificationTag)
            && !string.IsNullOrWhiteSpace(request.IntentDigest)
            && !string.IsNullOrWhiteSpace(request.ExecutionMessageId)
            && !string.IsNullOrWhiteSpace(request.ExecutionCorrelationId)
            && Enum.IsDefined(request.RetentionTier);

    private static bool IsVerifiable(
        IdempotencyAdmissionRecord record,
        IdempotencyAdmissionRequest request)
        => IsStructurallyValid(record)
            && string.Equals(record.TenantPartition, request.TenantPartition, StringComparison.Ordinal)
            && string.Equals(record.DigestKeyVersion, request.DigestKeyVersion, StringComparison.Ordinal)
            && string.Equals(record.KeyDigest, request.KeyDigest, StringComparison.Ordinal)
            && record.RetentionTier == request.RetentionTier;

    private static bool IsVerifiable(
        IdempotencyAdmissionTombstone tombstone,
        IdempotencyAdmissionRequest request)
        => IsStructurallyValid(tombstone)
            && string.Equals(tombstone.TenantPartition, request.TenantPartition, StringComparison.Ordinal)
            && string.Equals(tombstone.DigestKeyVersion, request.DigestKeyVersion, StringComparison.Ordinal)
            && string.Equals(tombstone.KeyDigest, request.KeyDigest, StringComparison.Ordinal)
            && tombstone.RetentionTier == request.RetentionTier;

    private static bool IsStructurallyValid(IdempotencyAdmissionRecord record)
        => record.SchemaVersion == IdempotencyAdmissionRecord.CurrentSchemaVersion
            && Enum.IsDefined(record.State)
            && !string.IsNullOrWhiteSpace(record.TenantPartition)
            && !string.IsNullOrWhiteSpace(record.DigestKeyVersion)
            && !string.IsNullOrWhiteSpace(record.KeyDigest)
            && !string.IsNullOrWhiteSpace(record.VerificationTag)
            && Enum.IsDefined(record.RetentionTier)
            && record.FencingToken > 0
            && record.LastObservedAt >= record.FirstConsumedAt
            && (record.State == IdempotencyAdmissionState.Expired
                ? record.ReplayExpiresAt is not null
                    && record.ReplayExpiresAt >= record.FirstConsumedAt
                : !string.IsNullOrWhiteSpace(record.IntentDigest)
                    && !string.IsNullOrWhiteSpace(record.ExecutionMessageId)
                    && !string.IsNullOrWhiteSpace(record.ExecutionCorrelationId)
                    && (record.State == IdempotencyAdmissionState.Terminal
                        ? record.ReplayResult is not null
                            && record.ReplayExpiresAt is not null
                            && record.ReplayExpiresAt >= record.FirstConsumedAt
                        : record.ReplayResult is null && record.ReplayExpiresAt is null));

    private static bool IsStructurallyValid(IdempotencyAdmissionTombstone tombstone)
        => tombstone.SchemaVersion == IdempotencyAdmissionTombstone.CurrentSchemaVersion
            && tombstone.State == IdempotencyAdmissionState.Expired
            && !string.IsNullOrWhiteSpace(tombstone.TenantPartition)
            && !string.IsNullOrWhiteSpace(tombstone.DigestKeyVersion)
            && !string.IsNullOrWhiteSpace(tombstone.KeyDigest)
            && !string.IsNullOrWhiteSpace(tombstone.VerificationTag)
            && Enum.IsDefined(tombstone.RetentionTier)
            && tombstone.ReplayExpiredAt >= tombstone.FirstConsumedAt
            && tombstone.LastObservedAt >= tombstone.ReplayExpiredAt;

    private static bool FixedTimeEquals(string? left, string? right)
    {
        if (left is null || right is null)
        {
            return false;
        }

        byte[] leftBytes = Encoding.UTF8.GetBytes(left);
        byte[] rightBytes = Encoding.UTF8.GetBytes(right);
        bool equals = leftBytes.Length == rightBytes.Length
            && CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
        CryptographicOperations.ZeroMemory(leftBytes);
        CryptographicOperations.ZeroMemory(rightBytes);
        return equals;
    }

    private static bool SameImportedRecord(
        IdempotencyAdmissionRecord left,
        IdempotencyAdmissionRecord right)
        => left.SchemaVersion == right.SchemaVersion
            && left.State == right.State
            && string.Equals(left.TenantPartition, right.TenantPartition, StringComparison.Ordinal)
            && string.Equals(left.DigestKeyVersion, right.DigestKeyVersion, StringComparison.Ordinal)
            && string.Equals(left.KeyDigest, right.KeyDigest, StringComparison.Ordinal)
            && FixedTimeEquals(left.VerificationTag, right.VerificationTag)
            && (left.IntentDigest is null
                ? right.IntentDigest is null
                : FixedTimeEquals(left.IntentDigest, right.IntentDigest))
            && left.RetentionTier == right.RetentionTier
            && left.FirstConsumedAt == right.FirstConsumedAt
            && left.LastObservedAt == right.LastObservedAt
            && left.ReplayExpiresAt == right.ReplayExpiresAt
            && left.FencingToken == right.FencingToken
            && Equals(left.ReplayResult, right.ReplayResult)
            && string.Equals(left.ExecutionMessageId, right.ExecutionMessageId, StringComparison.Ordinal)
            && string.Equals(left.ExecutionCorrelationId, right.ExecutionCorrelationId, StringComparison.Ordinal);

    private static bool SameImportedTombstone(
        IdempotencyAdmissionTombstone left,
        IdempotencyAdmissionTombstone right)
        => left.SchemaVersion == right.SchemaVersion
            && left.State == right.State
            && string.Equals(left.TenantPartition, right.TenantPartition, StringComparison.Ordinal)
            && string.Equals(left.DigestKeyVersion, right.DigestKeyVersion, StringComparison.Ordinal)
            && string.Equals(left.KeyDigest, right.KeyDigest, StringComparison.Ordinal)
            && FixedTimeEquals(left.VerificationTag, right.VerificationTag)
            && left.RetentionTier == right.RetentionTier
            && left.FirstConsumedAt == right.FirstConsumedAt
            && left.ReplayExpiredAt == right.ReplayExpiredAt
            && left.LastObservedAt == right.LastObservedAt;

    private async Task<IdempotencyAdmissionRecord> LoadRequiredAsync()
    {
        ConditionalValue<IdempotencyAdmissionRecord> stored = await StateManager
            .TryGetStateAsync<IdempotencyAdmissionRecord>(StateName)
            .ConfigureAwait(false);
        return stored.HasValue
            ? stored.Value
            : throw new InvalidOperationException("The idempotency admission record is missing.");
    }

    private async Task EnsureAdmissionExistsAsync()
    {
        ConditionalValue<IdempotencyAdmissionRecord> record = await StateManager
            .TryGetStateAsync<IdempotencyAdmissionRecord>(StateName)
            .ConfigureAwait(false);
        ConditionalValue<IdempotencyAdmissionTombstone> tombstone = await StateManager
            .TryGetStateAsync<IdempotencyAdmissionTombstone>(TombstoneStateName)
            .ConfigureAwait(false);
        if (record.HasValue == tombstone.HasValue)
        {
            throw new InvalidOperationException("Exactly one idempotency admission state entry is required.");
        }
    }

    private async Task PersistAsync(IdempotencyAdmissionRecord record)
    {
        await StateManager.SetStateAsync(StateName, record).ConfigureAwait(false);
        await StateManager.SaveStateAsync().ConfigureAwait(false);
    }

    private async Task PersistAsync(IdempotencyAdmissionTombstone tombstone)
    {
        await StateManager.SetStateAsync(TombstoneStateName, tombstone).ConfigureAwait(false);
        await StateManager.SaveStateAsync().ConfigureAwait(false);
    }

    private async Task CompactAsync(IdempotencyAdmissionRecord record, DateTimeOffset observedAt)
    {
        DateTimeOffset replayExpiredAt = record.ReplayExpiresAt
            ?? throw new InvalidOperationException("Expired admission state has no replay-expiry boundary.");
        var tombstone = new IdempotencyAdmissionTombstone(
            IdempotencyAdmissionTombstone.CurrentSchemaVersion,
            IdempotencyAdmissionState.Expired,
            record.TenantPartition,
            record.KeyDigest,
            record.VerificationTag,
            record.DigestKeyVersion,
            record.RetentionTier,
            record.FirstConsumedAt,
            replayExpiredAt,
            observedAt);
        await StateManager.SetStateAsync(TombstoneStateName, tombstone).ConfigureAwait(false);
        _ = await StateManager.TryRemoveStateAsync(StateName).ConfigureAwait(false);
        await StateManager.SaveStateAsync().ConfigureAwait(false);
    }

    private static DateTimeOffset Max(DateTimeOffset left, DateTimeOffset right)
        => left >= right ? left : right;

    private static partial class Log
    {
        [LoggerMessage(EventId = 5050, Level = LogLevel.Information, Message = "Idempotency key reserved. Stage=IdempotencyReserved")]
        public static partial void Reserved(ILogger logger);

        [LoggerMessage(EventId = 5051, Level = LogLevel.Debug, Message = "Idempotency execution began. Stage=IdempotencyPending")]
        public static partial void Began(ILogger logger);

        [LoggerMessage(EventId = 5052, Level = LogLevel.Debug, Message = "Idempotency result finalized. Stage=IdempotencyTerminal")]
        public static partial void Completed(ILogger logger);

        [LoggerMessage(EventId = 5053, Level = LogLevel.Information, Message = "Idempotency result expired. Stage=IdempotencyExpired")]
        public static partial void Expired(ILogger logger);

        [LoggerMessage(EventId = 5054, Level = LogLevel.Warning, Message = "Idempotency intent conflict. Stage=IdempotencyConflict")]
        public static partial void Conflict(ILogger logger);

        [LoggerMessage(EventId = 5055, Level = LogLevel.Error, Message = "Idempotency state failed verification. Stage=IdempotencyCorrupt")]
        public static partial void CorruptState(ILogger logger);

        [LoggerMessage(EventId = 5056, Level = LogLevel.Debug, Message = "Idempotency request classified. Decision={Decision}, Stage=IdempotencyClassified")]
        public static partial void Classified(ILogger logger, string decision);

        [LoggerMessage(EventId = 5057, Level = LogLevel.Warning, Message = "Idempotency recovery state recorded. State={State}, Stage=IdempotencyRecovery")]
        public static partial void RecoveryMarked(ILogger logger, string state);

        [LoggerMessage(EventId = 5058, Level = LogLevel.Error, Message = "Idempotency partition verification collision detected. Stage=IdempotencyCollision")]
        public static partial void Collision(ILogger logger);
    }
}
