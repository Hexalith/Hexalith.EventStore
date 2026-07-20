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

    private TimeProvider Clock { get; } = timeProvider ?? TimeProvider.System;

    /// <inheritdoc/>
    public async Task<IdempotencyAdmissionResult> AdmitAsync(IdempotencyAdmissionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!IsValidRequest(request))
        {
            Log.CorruptState(logger);
            return new IdempotencyAdmissionResult(IdempotencyAdmissionDecision.Corrupt);
        }

        ConditionalValue<IdempotencyAdmissionRecord> stored = await StateManager
            .TryGetStateAsync<IdempotencyAdmissionRecord>(StateName)
            .ConfigureAwait(false);
        DateTimeOffset now = Clock.GetUtcNow();
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
                ReplayResult: null);
            await PersistAsync(reserved).ConfigureAwait(false);
            Log.Reserved(logger);
            return new IdempotencyAdmissionResult(IdempotencyAdmissionDecision.Execute, reserved.FencingToken);
        }

        IdempotencyAdmissionRecord record = stored.Value;
        if (!IsVerifiable(record, request))
        {
            Log.CorruptState(logger);
            return new IdempotencyAdmissionResult(IdempotencyAdmissionDecision.Corrupt);
        }

        DateTimeOffset effectiveNow = now > record.LastObservedAt ? now : record.LastObservedAt;
        if (record.State == IdempotencyAdmissionState.Expired)
        {
            if (effectiveNow > record.LastObservedAt)
            {
                await PersistAsync(record with { LastObservedAt = effectiveNow }).ConfigureAwait(false);
            }

            Log.Expired(logger);
            return new IdempotencyAdmissionResult(IdempotencyAdmissionDecision.Expired, record.FencingToken);
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
                IdempotencyAdmissionRecord compacted = record with
                {
                    State = IdempotencyAdmissionState.Expired,
                    IntentDigest = null,
                    ReplayResult = null,
                    LastObservedAt = effectiveNow,
                };
                await PersistAsync(compacted).ConfigureAwait(false);
                Log.Expired(logger);
                return new IdempotencyAdmissionResult(IdempotencyAdmissionDecision.Expired, record.FencingToken);
            }
        }

        if (!FixedTimeEquals(record.IntentDigest, request.IntentDigest))
        {
            Log.Conflict(logger);
            return new IdempotencyAdmissionResult(IdempotencyAdmissionDecision.Conflict, record.FencingToken);
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
            decision == IdempotencyAdmissionDecision.Replay ? record.ReplayResult : null);
    }

    /// <inheritdoc/>
    public async Task BeginAsync(IdempotencyAdmissionTransitionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        IdempotencyAdmissionRecord record = await LoadRequiredAsync().ConfigureAwait(false);
        EnsureFence(record, request.FencingToken);
        if (record.State != IdempotencyAdmissionState.Reserved)
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
        if (record.State is not (IdempotencyAdmissionState.Pending or IdempotencyAdmissionState.Recoverable))
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
            && Enum.IsDefined(request.RetentionTier);

    private static bool IsVerifiable(
        IdempotencyAdmissionRecord record,
        IdempotencyAdmissionRequest request)
        => record.SchemaVersion == IdempotencyAdmissionRecord.CurrentSchemaVersion
            && string.Equals(record.TenantPartition, request.TenantPartition, StringComparison.Ordinal)
            && string.Equals(record.DigestKeyVersion, request.DigestKeyVersion, StringComparison.Ordinal)
            && string.Equals(record.KeyDigest, request.KeyDigest, StringComparison.Ordinal)
            && FixedTimeEquals(record.VerificationTag, request.VerificationTag)
            && record.RetentionTier == request.RetentionTier
            && Enum.IsDefined(record.State);

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

    private async Task<IdempotencyAdmissionRecord> LoadRequiredAsync()
    {
        ConditionalValue<IdempotencyAdmissionRecord> stored = await StateManager
            .TryGetStateAsync<IdempotencyAdmissionRecord>(StateName)
            .ConfigureAwait(false);
        return stored.HasValue
            ? stored.Value
            : throw new InvalidOperationException("The idempotency admission record is missing.");
    }

    private async Task PersistAsync(IdempotencyAdmissionRecord record)
    {
        await StateManager.SetStateAsync(StateName, record).ConfigureAwait(false);
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
    }
}
