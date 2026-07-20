using Dapr.Actors.Runtime;

using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Owns the governed managed-tenant lifetime plus post-deletion retention interval.</summary>
public sealed class IdempotencyTenantLifecycleActor(
    ActorHost host,
    ILogger<IdempotencyTenantLifecycleActor> logger,
    TimeProvider? timeProvider = null)
    : Actor(host), IIdempotencyTenantLifecycleActor
{
    /// <summary>Gets the Dapr actor type name.</summary>
    public const string ActorTypeName = nameof(IdempotencyTenantLifecycleActor);

    /// <summary>Gets the fixed lifecycle state name.</summary>
    public const string StateName = "lifecycle";

    private TimeProvider Clock { get; } = timeProvider ?? TimeProvider.System;

    private ILogger<IdempotencyTenantLifecycleActor> LifecycleLogger { get; } = logger;

    /// <inheritdoc/>
    public async Task RegisterAsync(IdempotencyTenantLifecycleReference[] references)
    {
        ArgumentNullException.ThrowIfNull(references);
        if (references.Length == 0 || references.Any(static reference =>
            reference is null
            || string.IsNullOrWhiteSpace(reference.ActorId)
            || string.IsNullOrWhiteSpace(reference.DigestKeyVersion)
            || string.IsNullOrWhiteSpace(reference.KeyDigest)))
        {
            throw new InvalidOperationException("Idempotency lifecycle references are invalid.");
        }

        IdempotencyTenantLifecycleRecord record = await LoadOrCreateAsync().ConfigureAwait(false);
        IdempotencyTenantLifecycleReference[][] groups = record.References
            .Concat(references)
            .GroupBy(static reference => reference.ActorId, StringComparer.Ordinal)
            .Select(static group => group.ToArray())
            .ToArray();
        if (groups.Any(static group => group.Skip(1).Any(reference =>
            !string.Equals(reference.DigestKeyVersion, group[0].DigestKeyVersion, StringComparison.Ordinal)
            || !string.Equals(reference.KeyDigest, group[0].KeyDigest, StringComparison.Ordinal))))
        {
            throw new InvalidOperationException("One idempotency lifecycle actor reference has conflicting protected identity.");
        }

        IdempotencyTenantLifecycleReference[] merged = groups
            .Select(static group => group[0])
            .OrderBy(static reference => reference.ActorId, StringComparer.Ordinal)
            .ToArray();
        if (merged.Length != record.References.Length
            && record.State != IdempotencyTenantLifecycleState.Active)
        {
            throw new InvalidOperationException("Tenant deletion lifecycle forbids new idempotency state.");
        }

        if (merged.Length != record.References.Length)
        {
            await PersistAsync(record with
            {
                References = merged,
                LastObservedAt = Max(record.LastObservedAt, Clock.GetUtcNow()),
            }).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async Task<IdempotencyTenantLifecycleRecord> EnterDeletionAsync(DateTimeOffset approvedAt)
    {
        IdempotencyTenantLifecycleRecord record = await LoadOrCreateAsync().ConfigureAwait(false);
        if (record.State != IdempotencyTenantLifecycleState.Active)
        {
            return await RefreshAsync(record).ConfigureAwait(false);
        }

        DateTimeOffset effective = Max(Max(record.LastObservedAt, Clock.GetUtcNow()), approvedAt);
        DateTimeOffset deleteAfter = approvedAt.Add(IdempotencyTenantLifecycleRecord.PostDeletionRetention);
        TimeSpan remaining = deleteAfter > effective
            ? deleteAfter - effective
            : TimeSpan.Zero;
        return await PersistAsync(record with
        {
            State = remaining == TimeSpan.Zero
                ? IdempotencyTenantLifecycleState.PurgeEligible
                : IdempotencyTenantLifecycleState.Retaining,
            LastObservedAt = effective,
            DeletionApprovedAt = approvedAt,
            DeleteAfter = deleteAfter,
            RemainingRetention = remaining,
        }).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IdempotencyTenantLifecycleRecord> PlaceLegalHoldAsync(DateTimeOffset observedAt)
    {
        IdempotencyTenantLifecycleRecord record = await RefreshAsync(await LoadRequiredAsync().ConfigureAwait(false))
            .ConfigureAwait(false);
        if (record.State == IdempotencyTenantLifecycleState.LegalHold)
        {
            return record;
        }

        if (record.State is not (IdempotencyTenantLifecycleState.Retaining
            or IdempotencyTenantLifecycleState.PurgeEligible))
        {
            throw new InvalidOperationException("Legal hold requires an approved tenant deletion workflow.");
        }

        DateTimeOffset effective = Max(Max(record.LastObservedAt, Clock.GetUtcNow()), observedAt);
        TimeSpan remaining = record.DeleteAfter > effective
            ? record.DeleteAfter.Value - effective
            : TimeSpan.Zero;
        return await PersistAsync(record with
        {
            State = IdempotencyTenantLifecycleState.LegalHold,
            LastObservedAt = effective,
            RemainingRetention = remaining,
            LegalHoldStartedAt = effective,
        }).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IdempotencyTenantLifecycleRecord> ReleaseLegalHoldAsync(DateTimeOffset observedAt)
    {
        IdempotencyTenantLifecycleRecord record = await LoadRequiredAsync().ConfigureAwait(false);
        if (record.State != IdempotencyTenantLifecycleState.LegalHold)
        {
            throw new InvalidOperationException("The tenant is not under legal hold.");
        }

        DateTimeOffset effective = Max(Max(record.LastObservedAt, Clock.GetUtcNow()), observedAt);
        TimeSpan remaining = record.RemainingRetention ?? TimeSpan.Zero;
        return await PersistAsync(record with
        {
            State = remaining == TimeSpan.Zero
                ? IdempotencyTenantLifecycleState.PurgeEligible
                : IdempotencyTenantLifecycleState.Retaining,
            LastObservedAt = effective,
            DeleteAfter = effective.Add(remaining),
            LegalHoldStartedAt = null,
        }).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IdempotencyTenantLifecycleRecord> GetAsync()
        => await RefreshAsync(await LoadOrCreateAsync().ConfigureAwait(false)).ConfigureAwait(false);

    /// <inheritdoc/>
    public async Task<IdempotencyTenantLifecycleRecord> AcknowledgePurgeAsync(string actorId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);
        IdempotencyTenantLifecycleRecord record = await RefreshAsync(await LoadRequiredAsync().ConfigureAwait(false))
            .ConfigureAwait(false);
        if (record.State != IdempotencyTenantLifecycleState.PurgeEligible)
        {
            throw new InvalidOperationException("Tenant idempotency state is not purge eligible.");
        }

        IdempotencyTenantLifecycleReference[] remaining = record.References
            .Where(reference => !string.Equals(reference.ActorId, actorId, StringComparison.Ordinal))
            .ToArray();
        if (remaining.Length == record.References.Length)
        {
            throw new InvalidOperationException("The purged idempotency lifecycle reference is not registered.");
        }

        return await PersistAsync(record with
        {
            State = remaining.Length == 0
                ? IdempotencyTenantLifecycleState.Purged
                : IdempotencyTenantLifecycleState.PurgeEligible,
            References = remaining,
            LastObservedAt = Max(record.LastObservedAt, Clock.GetUtcNow()),
        }).ConfigureAwait(false);
    }

    private async Task<IdempotencyTenantLifecycleRecord> RefreshAsync(IdempotencyTenantLifecycleRecord record)
    {
        DateTimeOffset effective = Max(record.LastObservedAt, Clock.GetUtcNow());
        IdempotencyTenantLifecycleState state = record.State == IdempotencyTenantLifecycleState.Retaining
            && record.DeleteAfter <= effective
                ? IdempotencyTenantLifecycleState.PurgeEligible
                : record.State;
        return effective > record.LastObservedAt || state != record.State
            ? await PersistAsync(record with { State = state, LastObservedAt = effective }).ConfigureAwait(false)
            : record;
    }

    private async Task<IdempotencyTenantLifecycleRecord> LoadOrCreateAsync()
    {
        ConditionalValue<IdempotencyTenantLifecycleRecord> stored = await StateManager
            .TryGetStateAsync<IdempotencyTenantLifecycleRecord>(StateName)
            .ConfigureAwait(false);
        if (stored.HasValue)
        {
            Validate(stored.Value);
            return stored.Value;
        }

        DateTimeOffset now = Clock.GetUtcNow();
        var created = new IdempotencyTenantLifecycleRecord(
            IdempotencyTenantLifecycleRecord.CurrentSchemaVersion,
            Host.Id.GetId(),
            IdempotencyTenantLifecycleState.Active,
            now,
            null,
            null,
            null,
            null,
            []);
        return await PersistAsync(created).ConfigureAwait(false);
    }

    private async Task<IdempotencyTenantLifecycleRecord> LoadRequiredAsync()
    {
        ConditionalValue<IdempotencyTenantLifecycleRecord> stored = await StateManager
            .TryGetStateAsync<IdempotencyTenantLifecycleRecord>(StateName)
            .ConfigureAwait(false);
        if (!stored.HasValue)
        {
            throw new InvalidOperationException("Tenant idempotency lifecycle state is missing.");
        }

        Validate(stored.Value);
        return stored.Value;
    }

    private async Task<IdempotencyTenantLifecycleRecord> PersistAsync(IdempotencyTenantLifecycleRecord record)
    {
        await StateManager.SetStateAsync(StateName, record).ConfigureAwait(false);
        await StateManager.SaveStateAsync().ConfigureAwait(false);
        LifecycleLogger.LogDebug(
            "Tenant idempotency lifecycle persisted. State={State}, ReferenceCount={ReferenceCount}, Stage=IdempotencyTenantLifecycle",
            record.State,
            record.References.Length);
        return record;
    }

    private static void Validate(IdempotencyTenantLifecycleRecord record)
    {
        if (record.SchemaVersion != IdempotencyTenantLifecycleRecord.CurrentSchemaVersion
            || string.IsNullOrWhiteSpace(record.Tenant)
            || !Enum.IsDefined(record.State)
            || record.References is null)
        {
            throw new InvalidOperationException("Tenant idempotency lifecycle state is corrupt.");
        }
    }

    private static DateTimeOffset Max(DateTimeOffset left, DateTimeOffset right)
        => left >= right ? left : right;
}
