using System.Security.Cryptography;
using System.Text;

using Dapr.Actors;
using Dapr.Actors.Client;
using Dapr.Actors.Runtime;

using Hexalith.EventStore.Server.Configuration;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Owns the governed managed-tenant lifetime plus post-deletion retention interval.</summary>
public sealed class IdempotencyTenantLifecycleActor(
    ActorHost host,
    ILogger<IdempotencyTenantLifecycleActor> logger,
    TimeProvider? timeProvider = null,
    IActorProxyFactory? actorProxyFactory = null,
    IOptions<EventStoreActorOptions>? actorOptions = null)
    : Actor(host), IIdempotencyTenantLifecycleActor, IIdempotencyTenantLifecycleMigrationActor
{
    /// <summary>Gets the maximum number of references removed in one serialized actor turn.</summary>
    public const int MaximumReferencesPerPurgeTurn = 1;

    /// <summary>Gets the Dapr actor type name.</summary>
    public const string ActorTypeName = nameof(IdempotencyTenantLifecycleActor);

    /// <summary>Gets the fixed lifecycle state name.</summary>
    public const string StateName = "lifecycle";

    private TimeProvider Clock { get; } = timeProvider ?? TimeProvider.System;

    private ILogger<IdempotencyTenantLifecycleActor> LifecycleLogger { get; } = logger;

    private string AggregateActorTypeName { get; }
        = actorOptions?.Value.AggregateActorTypeName ?? nameof(AggregateActor);

    /// <inheritdoc/>
    public async Task RegisterAsync(IdempotencyTenantLifecycleReference[] references)
    {
        ArgumentNullException.ThrowIfNull(references);
        if (references.Length == 0
            || references.Any(reference => !IsValidReference(reference, Host.Id.GetId())))
        {
            throw new InvalidOperationException("Idempotency lifecycle references are invalid.");
        }

        IdempotencyTenantLifecycleRecord record = await LoadOrCreateAsync().ConfigureAwait(false);
        if (record.State != IdempotencyTenantLifecycleState.Active)
        {
            throw new InvalidOperationException("Tenant deletion lifecycle forbids idempotency admission.");
        }

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
    public async Task<IdempotencyAdmissionResult> AdmitAsync(IdempotencyTenantLifecycleAdmissionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Reference);
        ArgumentNullException.ThrowIfNull(request.Admission);
        IdempotencyTenantLifecycleRecord record = await LoadRequiredAsync().ConfigureAwait(false);
        if (record.State != IdempotencyTenantLifecycleState.Active)
        {
            throw new InvalidOperationException("Tenant deletion lifecycle forbids idempotency admission.");
        }

        IdempotencyTenantLifecycleReference reference = request.Reference;
        IdempotencyAdmissionRequest admission = request.Admission;
        bool registered = record.References.Any(candidate =>
            string.Equals(candidate.ActorId, reference.ActorId, StringComparison.Ordinal)
            && string.Equals(candidate.DigestKeyVersion, reference.DigestKeyVersion, StringComparison.Ordinal)
            && string.Equals(candidate.KeyDigest, reference.KeyDigest, StringComparison.Ordinal));
        if (!registered
            || !IsValidReference(reference, record.Tenant)
            || !string.Equals(admission.TenantPartition, record.Tenant, StringComparison.Ordinal)
            || !string.Equals(admission.DigestKeyVersion, reference.DigestKeyVersion, StringComparison.Ordinal)
            || !string.Equals(admission.KeyDigest, reference.KeyDigest, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The protected admission is not registered for the active tenant lifecycle.");
        }

        IActorProxyFactory factory = actorProxyFactory
            ?? throw new InvalidOperationException("Serialized idempotency lifecycle admission is unavailable.");
        IIdempotencyAdmissionActor actor = factory.CreateActorProxy<IIdempotencyAdmissionActor>(
            new ActorId(reference.ActorId),
            IdempotencyAdmissionActor.ActorTypeName);
        return await actor.AdmitAsync(admission).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    async Task<IdempotencyLegacyMigrationResult> IIdempotencyTenantLifecycleMigrationActor.MigrateLegacyAsync(
        IdempotencyLegacyMigrationRequest request)
    {
        ValidateMigrationRequest(request);
        IdempotencyTenantLifecycleRecord lifecycle = await LoadRequiredAsync().ConfigureAwait(false);
        if (lifecycle.State != IdempotencyTenantLifecycleState.Active)
        {
            throw new InvalidOperationException("Tenant deletion lifecycle forbids legacy migration.");
        }

        if (!lifecycle.References.Any(reference => Equals(reference, request.Target))
            || !request.Aliases.Any(alias =>
                string.Equals(alias.ActorId, request.Target.ActorId, StringComparison.Ordinal)
                && string.Equals(alias.DigestKeyVersion, request.Target.DigestKeyVersion, StringComparison.Ordinal)
                && string.Equals(alias.KeyDigest, request.Target.KeyDigest, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("The pinned legacy migration target is not registered for this tenant.");
        }

        IActorProxyFactory factory = actorProxyFactory
            ?? throw new InvalidOperationException("Serialized legacy migration is unavailable.");
        IIdempotencyLegacyInventoryActor inventory = factory
            .CreateActorProxy<IIdempotencyLegacyInventoryActor>(
                new ActorId(lifecycle.Tenant),
                IdempotencyLegacyInventoryActor.ActorTypeName);
        IdempotencyLegacyInventoryInspection inventoryInspection = await inventory
            .InspectAsync(request.Aliases).ConfigureAwait(false);
        if (inventoryInspection.Decision is not (IdempotencyLegacyInventoryDecision.Migrate
            or IdempotencyLegacyInventoryDecision.Migrated)
            || inventoryInspection.Entry is null)
        {
            return new IdempotencyLegacyMigrationResult(
                request.Target.ActorId,
                IdempotencyAdmissionDecision.UnsafeLegacy);
        }

        IdempotencyLegacyInventoryEntry entry = inventoryInspection.Entry;
        IdempotencyAdmissionDirectoryAlias[] matchingSourceAliases = request.Aliases
            .Where(alias =>
                string.Equals(alias.DigestKeyVersion, entry.DigestKeyVersion, StringComparison.Ordinal)
                && string.Equals(alias.KeyDigest, entry.KeyDigest, StringComparison.Ordinal))
            .ToArray();
        if (!string.Equals(entry.TenantPartition, lifecycle.Tenant, StringComparison.Ordinal)
            || matchingSourceAliases.Length != 1)
        {
            return new IdempotencyLegacyMigrationResult(
                request.Target.ActorId,
                IdempotencyAdmissionDecision.UnsafeLegacy);
        }

        if (!FixedTimeEquals(entry.VerificationTag, request.SourceVerificationTag))
        {
            return new IdempotencyLegacyMigrationResult(
                request.Target.ActorId,
                IdempotencyAdmissionDecision.Collision);
        }

        if (!FixedTimeEquals(entry.IntentDigest, request.SourceIntentDigest)
            || entry.RetentionTier != request.SourceRetentionTier
            || entry.RetentionTier != request.TargetRetentionTier)
        {
            return new IdempotencyLegacyMigrationResult(
                request.Target.ActorId,
                IdempotencyAdmissionDecision.Conflict);
        }

        string targetActorId = entry.TargetAdmissionActorId ?? request.Target.ActorId;
        if (!string.Equals(targetActorId, request.Target.ActorId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Legacy migration must resume its pinned target authority.");
        }

        var sourceRequest = new IdempotencyLegacySourceRequest(
            IdempotencyLegacySourceRequest.CurrentSchemaVersion,
            entry.TenantPartition,
            entry.InventoryId,
            entry.MigrationId,
            entry.LegacySchemaVersion,
            entry.SourceEvidenceDigest,
            entry.ExecutionMessageId,
            entry.ExecutionCorrelationId,
            entry.FirstConsumedAt,
            entry.ReplayExpiresAt,
            entry.ReplayResult);
        IIdempotencyLegacySourceActor source = factory.CreateActorProxy<IIdempotencyLegacySourceActor>(
            new ActorId(entry.SourceAggregateActorId),
            AggregateActorTypeName);
        IIdempotencyAdmissionActor target = factory.CreateActorProxy<IIdempotencyAdmissionActor>(
            new ActorId(targetActorId),
            IdempotencyAdmissionActor.ActorTypeName);
        IdempotencyAdmissionRecord? targetRecord = null;
        IdempotencyAdmissionTombstone? targetTombstone = null;
        string targetImportDigest;
        if (entry.Phase == IdempotencyLegacyMigrationPhase.Inventoried)
        {
            (targetRecord, targetTombstone) = CreateTargetState(entry, request);
            targetImportDigest = IdempotencyAdmissionPromotionEvidence.Compute(
                targetRecord,
                targetTombstone);
        }
        else
        {
            targetImportDigest = entry.TargetImportDigest
                ?? throw new InvalidOperationException("The pinned legacy migration target digest is missing.");
        }

        var acknowledgementRequest = new IdempotencyAdmissionPromotionAcknowledgementRequest(
            entry.SourceAggregateActorId,
            entry.MigrationId,
            entry.SourceEvidenceDigest,
            targetImportDigest);
        for (int step = 0; step < 6; step++)
        {
            switch (entry.Phase)
            {
                case IdempotencyLegacyMigrationPhase.Inventoried:
                    IdempotencyLegacySourceInspection exactSource = await InspectSourceAsync(
                        source,
                        sourceRequest).ConfigureAwait(false);
                    IdempotencyAdmissionDecision? sourceDenial = ClassifySourceProof(
                        exactSource,
                        allowRedirect: false,
                        expectedRedirectDigest: null);
                    if (sourceDenial is not null)
                    {
                        return new IdempotencyLegacyMigrationResult(targetActorId, sourceDenial);
                    }

                    await target.PreparePromotionAsync(
                        new IdempotencyAdmissionPromotionImportRequest(
                            entry.SourceAggregateActorId,
                            targetRecord,
                            targetTombstone,
                            entry.MigrationId,
                            entry.SourceEvidenceDigest)).ConfigureAwait(false);
                    entry = await inventory.AdvanceAsync(CreateAdvanceRequest(
                        entry,
                        targetActorId,
                        targetImportDigest)).ConfigureAwait(false);
                    break;
                case IdempotencyLegacyMigrationPhase.TargetPrepared:
                    IdempotencyAdmissionPromotionAcknowledgement acknowledgement = await target
                        .AcknowledgePromotionAsync(acknowledgementRequest).ConfigureAwait(false);
                    if (acknowledgement.Activated)
                    {
                        throw new InvalidOperationException("Legacy migration target activated before source redirect.");
                    }

                    entry = await inventory.AdvanceAsync(CreateAdvanceRequest(
                        entry,
                        targetActorId,
                        targetImportDigest)).ConfigureAwait(false);
                    break;
                case IdempotencyLegacyMigrationPhase.TargetAcknowledged:
                    IdempotencyLegacySourceInspection redirected = await RedirectSourceAsync(
                        source,
                        new IdempotencyLegacySourceRedirectRequest(
                            sourceRequest,
                            targetActorId)).ConfigureAwait(false);
                    IdempotencyAdmissionDecision? redirectDenial = ClassifySourceProof(
                        redirected,
                        allowRedirect: true,
                        expectedRedirectDigest: null);
                    if (redirectDenial is not null)
                    {
                        return new IdempotencyLegacyMigrationResult(targetActorId, redirectDenial);
                    }

                    entry = await inventory.AdvanceAsync(CreateAdvanceRequest(
                        entry,
                        targetActorId,
                        targetImportDigest,
                        redirected.RedirectDigest)).ConfigureAwait(false);
                    break;
                case IdempotencyLegacyMigrationPhase.SourceRedirected:
                    IdempotencyAdmissionDecision? redirectedDenial = await ReproveSourceRedirectAsync(
                        source,
                        sourceRequest,
                        entry).ConfigureAwait(false);
                    if (redirectedDenial is not null)
                    {
                        return new IdempotencyLegacyMigrationResult(targetActorId, redirectedDenial);
                    }

                    _ = await RequireTargetAcknowledgementAsync(
                        target,
                        acknowledgementRequest,
                        activated: false).ConfigureAwait(false);
                    await RequireCanonicalDirectoryAsync(
                        factory,
                        lifecycle.Tenant,
                        request.Aliases,
                        targetActorId).ConfigureAwait(false);
                    entry = await inventory.AdvanceAsync(CreateAdvanceRequest(
                        entry,
                        targetActorId,
                        targetImportDigest,
                        entry.SourceRedirectDigest)).ConfigureAwait(false);
                    break;
                case IdempotencyLegacyMigrationPhase.AuthorityFlipped:
                    IdempotencyAdmissionDecision? flippedDenial = await ReproveSourceRedirectAsync(
                        source,
                        sourceRequest,
                        entry).ConfigureAwait(false);
                    if (flippedDenial is not null)
                    {
                        return new IdempotencyLegacyMigrationResult(targetActorId, flippedDenial);
                    }

                    IdempotencyAdmissionPromotionAcknowledgement activation = await target
                        .AcknowledgePromotionAsync(acknowledgementRequest).ConfigureAwait(false);
                    if (!activation.Activated)
                    {
                        await target.ActivatePromotionAsync(
                            new IdempotencyAdmissionPromotionActivationRequest(
                                entry.SourceAggregateActorId,
                                entry.MigrationId,
                                targetImportDigest)).ConfigureAwait(false);
                    }

                    _ = await RequireTargetAcknowledgementAsync(
                        target,
                        acknowledgementRequest,
                        activated: true).ConfigureAwait(false);
                    await RequireCanonicalDirectoryAsync(
                        factory,
                        lifecycle.Tenant,
                        request.Aliases,
                        targetActorId).ConfigureAwait(false);
                    entry = await inventory.AdvanceAsync(CreateAdvanceRequest(
                        entry,
                        targetActorId,
                        targetImportDigest,
                        entry.SourceRedirectDigest)).ConfigureAwait(false);
                    break;
                case IdempotencyLegacyMigrationPhase.Migrated:
                    IdempotencyAdmissionDecision? migratedDenial = await ReproveSourceRedirectAsync(
                        source,
                        sourceRequest,
                        entry).ConfigureAwait(false);
                    if (migratedDenial is not null)
                    {
                        return new IdempotencyLegacyMigrationResult(targetActorId, migratedDenial);
                    }

                    try
                    {
                        _ = await RequireTargetAcknowledgementAsync(
                            target,
                            acknowledgementRequest,
                            activated: true).ConfigureAwait(false);
                        string? currentAuthority = await ReproveCurrentAuthorityAsync(
                            factory,
                            lifecycle.Tenant,
                            request.Aliases,
                            targetActorId).ConfigureAwait(false);
                        return currentAuthority is null
                            ? new IdempotencyLegacyMigrationResult(
                                targetActorId,
                                IdempotencyAdmissionDecision.UnsafeLegacy)
                            : new IdempotencyLegacyMigrationResult(currentAuthority);
                    }
                    catch (InvalidOperationException)
                    {
                        return new IdempotencyLegacyMigrationResult(
                            targetActorId,
                            IdempotencyAdmissionDecision.UnsafeLegacy);
                    }
                default:
                    return new IdempotencyLegacyMigrationResult(
                        targetActorId,
                        IdempotencyAdmissionDecision.UnsafeLegacy);
            }
        }

        throw new InvalidOperationException("Legacy migration did not reach its stable target authority.");
    }

    /// <inheritdoc/>
    async Task IIdempotencyTenantLifecycleMigrationActor.RollbackLegacyAsync(
        IdempotencyLegacyLifecycleRollbackRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        IdempotencyTenantLifecycleRecord lifecycle = await LoadRequiredAsync().ConfigureAwait(false);
        if (lifecycle.State != IdempotencyTenantLifecycleState.Active
            || request.Target is null
            || !lifecycle.References.Any(reference => Equals(reference, request.Target))
            || request.ExpectedPhase is not (IdempotencyLegacyMigrationPhase.TargetPrepared
                or IdempotencyLegacyMigrationPhase.TargetAcknowledged))
        {
            throw new InvalidOperationException("Tenant lifecycle forbids this legacy migration rollback.");
        }

        IActorProxyFactory factory = actorProxyFactory
            ?? throw new InvalidOperationException("Serialized legacy migration rollback is unavailable.");
        IIdempotencyAdmissionActor target = factory.CreateActorProxy<IIdempotencyAdmissionActor>(
            new ActorId(request.Target.ActorId),
            IdempotencyAdmissionActor.ActorTypeName);
        await target.RollbackPromotionAsync(
            new IdempotencyAdmissionPromotionRollbackRequest(
                request.SourceAggregateActorId,
                request.MigrationId,
                request.SourceEvidenceDigest,
                request.TargetImportDigest)).ConfigureAwait(false);
        IIdempotencyLegacyInventoryActor inventory = factory
            .CreateActorProxy<IIdempotencyLegacyInventoryActor>(
                new ActorId(lifecycle.Tenant),
                IdempotencyLegacyInventoryActor.ActorTypeName);
        _ = await inventory.RollbackAsync(
            new IdempotencyLegacyMigrationRollbackRequest(
                request.InventoryId,
                request.MigrationId,
                request.SourceDigestKeyVersion,
                request.SourceKeyDigest,
                request.ExpectedPhase,
                request.Target.ActorId,
                request.TargetImportDigest)).ConfigureAwait(false);
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
    public async Task<IdempotencyTenantLifecycleRecord> PurgeAsync(int maximumCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumCount);
        if (maximumCount > MaximumReferencesPerPurgeTurn)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumCount),
                $"A lifecycle purge turn removes at most {MaximumReferencesPerPurgeTurn} reference.");
        }

        IdempotencyTenantLifecycleRecord record = await RefreshAsync(await LoadRequiredAsync().ConfigureAwait(false))
            .ConfigureAwait(false);
        if (record.State != IdempotencyTenantLifecycleState.PurgeEligible)
        {
            throw new InvalidOperationException("Tenant idempotency state is not purge eligible.");
        }

        if (record.References.Length == 0)
        {
            return await PersistAsync(record with
            {
                State = IdempotencyTenantLifecycleState.Purged,
                LastObservedAt = Max(record.LastObservedAt, Clock.GetUtcNow()),
            }).ConfigureAwait(false);
        }

        IActorProxyFactory factory = actorProxyFactory
            ?? throw new InvalidOperationException("Serialized idempotency lifecycle purge is unavailable.");
        IIdempotencyAdmissionDirectoryActor directory = factory
            .CreateActorProxy<IIdempotencyAdmissionDirectoryActor>(
                new ActorId(record.Tenant),
                IdempotencyAdmissionDirectoryActor.ActorTypeName);
        IIdempotencyLegacyInventoryActor inventory = factory
            .CreateActorProxy<IIdempotencyLegacyInventoryActor>(
                new ActorId(record.Tenant),
                IdempotencyLegacyInventoryActor.ActorTypeName);
        foreach (IdempotencyTenantLifecycleReference reference in record.References.Take(maximumCount))
        {
            IIdempotencyAdmissionActor admission = factory.CreateActorProxy<IIdempotencyAdmissionActor>(
                new ActorId(reference.ActorId),
                IdempotencyAdmissionActor.ActorTypeName);
            bool purged = await admission.PurgeTombstoneAsync(
                new IdempotencyAdmissionPurgeRequest(
                    record.Tenant,
                    reference.DigestKeyVersion,
                    reference.KeyDigest)).ConfigureAwait(false);
            if (!purged)
            {
                continue;
            }

            await directory.PurgeAliasAsync(
                new IdempotencyAdmissionDirectoryAlias(
                    reference.DigestKeyVersion,
                    reference.ActorId,
                    reference.KeyDigest)).ConfigureAwait(false);
            await inventory.PurgeAsync(
                new IdempotencyAdmissionDirectoryAlias(
                    reference.DigestKeyVersion,
                    reference.ActorId,
                    reference.KeyDigest)).ConfigureAwait(false);
            record = await AcknowledgePurgeCoreAsync(record, reference.ActorId).ConfigureAwait(false);
        }

        return record;
    }

    /// <inheritdoc/>
    public Task<IdempotencyTenantLifecycleRecord> AcknowledgePurgeAsync(string actorId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);
        throw new InvalidOperationException(
            "Direct purge acknowledgement is forbidden; serialized purge must remove the tombstone and alias first.");
    }

    private async Task<IdempotencyTenantLifecycleRecord> AcknowledgePurgeCoreAsync(
        IdempotencyTenantLifecycleRecord record,
        string actorId)
    {
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
        TimeSpan? remaining = state switch
        {
            IdempotencyTenantLifecycleState.Retaining => record.DeleteAfter!.Value - effective,
            IdempotencyTenantLifecycleState.PurgeEligible or IdempotencyTenantLifecycleState.Purged => TimeSpan.Zero,
            _ => record.RemainingRetention,
        };
        return effective > record.LastObservedAt || state != record.State || remaining != record.RemainingRetention
            ? await PersistAsync(record with
            {
                State = state,
                LastObservedAt = effective,
                RemainingRetention = remaining,
            }).ConfigureAwait(false)
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

    private void Validate(IdempotencyTenantLifecycleRecord record)
    {
        if (record.SchemaVersion != IdempotencyTenantLifecycleRecord.CurrentSchemaVersion
            || string.IsNullOrWhiteSpace(record.Tenant)
            || !string.Equals(record.Tenant, Host.Id.GetId(), StringComparison.Ordinal)
            || !Enum.IsDefined(record.State)
            || record.References is null
            || record.References.Any(reference => !IsValidReference(reference, record.Tenant))
            || record.References.Select(static reference => reference.ActorId)
                .Distinct(StringComparer.Ordinal).Count() != record.References.Length
            || !HasValidLifecycleShape(record))
        {
            throw new InvalidOperationException("Tenant idempotency lifecycle state is corrupt.");
        }
    }

    private static bool HasValidLifecycleShape(IdempotencyTenantLifecycleRecord record)
    {
        if (record.State == IdempotencyTenantLifecycleState.Active)
        {
            return record.DeletionApprovedAt is null
                && record.DeleteAfter is null
                && record.RemainingRetention is null
                && record.LegalHoldStartedAt is null;
        }

        if (record.DeletionApprovedAt is null
            || record.DeleteAfter is null
            || record.LastObservedAt < record.DeletionApprovedAt
            || record.DeleteAfter < record.DeletionApprovedAt.Value.Add(IdempotencyTenantLifecycleRecord.PostDeletionRetention)
            || record.RemainingRetention is null
            || record.RemainingRetention < TimeSpan.Zero
            || record.RemainingRetention > IdempotencyTenantLifecycleRecord.PostDeletionRetention)
        {
            return false;
        }

        return record.State switch
        {
            IdempotencyTenantLifecycleState.Retaining
                => record.RemainingRetention > TimeSpan.Zero
                    && record.DeleteAfter > record.LastObservedAt
                    && record.RemainingRetention == record.DeleteAfter - record.LastObservedAt
                    && record.LegalHoldStartedAt is null,
            IdempotencyTenantLifecycleState.LegalHold
                => record.LegalHoldStartedAt is not null
                    && record.LegalHoldStartedAt >= record.DeletionApprovedAt
                    && record.LegalHoldStartedAt <= record.LastObservedAt,
            IdempotencyTenantLifecycleState.PurgeEligible
                => record.RemainingRetention == TimeSpan.Zero
                    && record.DeleteAfter <= record.LastObservedAt
                    && record.LegalHoldStartedAt is null,
            IdempotencyTenantLifecycleState.Purged
                => record.RemainingRetention == TimeSpan.Zero
                    && record.DeleteAfter <= record.LastObservedAt
                    && record.LegalHoldStartedAt is null
                    && record.References.Length == 0,
            _ => false,
        };
    }

    private static bool IsValidReference(IdempotencyTenantLifecycleReference? reference, string tenant)
        => reference is not null
            && !string.IsNullOrWhiteSpace(reference.ActorId)
            && !string.IsNullOrWhiteSpace(reference.DigestKeyVersion)
            && !string.IsNullOrWhiteSpace(reference.KeyDigest)
            && string.Equals(
                reference.ActorId,
                IdempotencyAdmissionActorIdentity.Build(
                    tenant,
                    reference.DigestKeyVersion,
                    reference.KeyDigest),
                StringComparison.Ordinal);

    private void ValidateMigrationRequest(IdempotencyLegacyMigrationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Aliases is null
            || request.Aliases.Length == 0
            || request.Aliases.Any(alias => !IsValidAlias(alias, Host.Id.GetId()))
            || request.Aliases.Distinct().Count() != request.Aliases.Length
            || request.Target is null
            || !IsValidReference(request.Target, Host.Id.GetId())
            || string.IsNullOrWhiteSpace(request.TargetVerificationTag)
            || string.IsNullOrWhiteSpace(request.TargetIntentDigest)
            || string.IsNullOrWhiteSpace(request.SourceVerificationTag)
            || string.IsNullOrWhiteSpace(request.SourceIntentDigest)
            || !Enum.IsDefined(request.TargetRetentionTier)
            || !Enum.IsDefined(request.SourceRetentionTier))
        {
            throw new InvalidOperationException("Legacy migration lifecycle request is invalid.");
        }
    }

    private static bool IsValidAlias(IdempotencyAdmissionDirectoryAlias? alias, string tenant)
        => alias is not null
            && !string.IsNullOrWhiteSpace(alias.ActorId)
            && !string.IsNullOrWhiteSpace(alias.DigestKeyVersion)
            && !string.IsNullOrWhiteSpace(alias.KeyDigest)
            && string.Equals(
                alias.ActorId,
                IdempotencyAdmissionActorIdentity.Build(
                    tenant,
                    alias.DigestKeyVersion,
                    alias.KeyDigest),
                StringComparison.Ordinal);

    private (IdempotencyAdmissionRecord? Record, IdempotencyAdmissionTombstone? Tombstone) CreateTargetState(
        IdempotencyLegacyInventoryEntry entry,
        IdempotencyLegacyMigrationRequest request)
    {
        DateTimeOffset observedAt = Max(entry.LastObservedAt, Clock.GetUtcNow());
        if (observedAt >= entry.ReplayExpiresAt)
        {
            return (
                null,
                new IdempotencyAdmissionTombstone(
                    IdempotencyAdmissionTombstone.CurrentSchemaVersion,
                    IdempotencyAdmissionState.Expired,
                    entry.TenantPartition,
                    request.Target.KeyDigest,
                    request.TargetVerificationTag,
                    request.Target.DigestKeyVersion,
                    entry.RetentionTier,
                    entry.FirstConsumedAt,
                    entry.ReplayExpiresAt,
                    observedAt));
        }

        return (
            new IdempotencyAdmissionRecord(
                IdempotencyAdmissionRecord.CurrentSchemaVersion,
                IdempotencyAdmissionState.Terminal,
                entry.TenantPartition,
                request.Target.DigestKeyVersion,
                request.Target.KeyDigest,
                request.TargetVerificationTag,
                request.TargetIntentDigest,
                entry.RetentionTier,
                entry.FirstConsumedAt,
                observedAt,
                entry.ReplayExpiresAt,
                FencingToken: 1,
                entry.ReplayResult,
                entry.ExecutionMessageId,
                entry.ExecutionCorrelationId),
            null);
    }

    private static IdempotencyLegacyMigrationAdvanceRequest CreateAdvanceRequest(
        IdempotencyLegacyInventoryEntry entry,
        string targetActorId,
        string targetImportDigest,
        string? redirectDigest = null)
        => new(
            entry.InventoryId,
            entry.MigrationId,
            entry.DigestKeyVersion,
            entry.KeyDigest,
            entry.Phase,
            targetActorId,
            targetImportDigest,
            redirectDigest);

    private static IdempotencyAdmissionDecision? ClassifySourceProof(
        IdempotencyLegacySourceInspection inspection,
        bool allowRedirect,
        string? expectedRedirectDigest)
    {
        ArgumentNullException.ThrowIfNull(inspection);
        if (inspection.Decision is IdempotencyLegacySourceDecision.Exact
            or IdempotencyLegacySourceDecision.Expired)
        {
            return null;
        }

        if (allowRedirect
            && inspection.Decision == IdempotencyLegacySourceDecision.Redirected
            && !string.IsNullOrWhiteSpace(inspection.RedirectDigest)
            && (expectedRedirectDigest is null
                || string.Equals(
                    inspection.RedirectDigest,
                    expectedRedirectDigest,
                    StringComparison.Ordinal)))
        {
            return null;
        }

        return inspection.Decision switch
        {
            IdempotencyLegacySourceDecision.Conflict => IdempotencyAdmissionDecision.Conflict,
            IdempotencyLegacySourceDecision.Unavailable
                => throw new InvalidOperationException("Legacy source evidence is temporarily unavailable."),
            _ => IdempotencyAdmissionDecision.UnsafeLegacy,
        };
    }

    private static async Task<IdempotencyAdmissionDecision?> ReproveSourceRedirectAsync(
        IIdempotencyLegacySourceActor source,
        IdempotencyLegacySourceRequest sourceRequest,
        IdempotencyLegacyInventoryEntry entry)
    {
        IdempotencyLegacySourceInspection inspection = await InspectSourceAsync(
            source,
            sourceRequest).ConfigureAwait(false);
        if (inspection.Decision == IdempotencyLegacySourceDecision.Redirected
            && string.Equals(
                inspection.RedirectDigest,
                entry.SourceRedirectDigest,
                StringComparison.Ordinal))
        {
            return null;
        }

        return inspection.Decision switch
        {
            IdempotencyLegacySourceDecision.Conflict => IdempotencyAdmissionDecision.Conflict,
            IdempotencyLegacySourceDecision.Unavailable
                => throw new InvalidOperationException("Legacy source evidence is temporarily unavailable."),
            _ => IdempotencyAdmissionDecision.UnsafeLegacy,
        };
    }

    private static async Task<IdempotencyLegacySourceInspection> InspectSourceAsync(
        IIdempotencyLegacySourceActor source,
        IdempotencyLegacySourceRequest request)
    {
        try
        {
            return await source.InspectLegacySourceAsync(request).ConfigureAwait(false);
        }
        catch (Exception)
        {
            throw new InvalidOperationException("Legacy source evidence is unavailable or inconsistent.");
        }
    }

    private static async Task<IdempotencyLegacySourceInspection> RedirectSourceAsync(
        IIdempotencyLegacySourceActor source,
        IdempotencyLegacySourceRedirectRequest request)
    {
        try
        {
            return await source.SetLegacySourceRedirectAsync(request).ConfigureAwait(false);
        }
        catch (Exception)
        {
            throw new InvalidOperationException("Legacy source evidence is unavailable or inconsistent.");
        }
    }

    private static async Task<IdempotencyAdmissionPromotionAcknowledgement> RequireTargetAcknowledgementAsync(
        IIdempotencyAdmissionActor target,
        IdempotencyAdmissionPromotionAcknowledgementRequest request,
        bool activated)
    {
        IdempotencyAdmissionPromotionAcknowledgement acknowledgement = await target
            .AcknowledgePromotionAsync(request).ConfigureAwait(false);
        if (acknowledgement.Activated != activated)
        {
            throw new InvalidOperationException("Legacy migration target activation proof is inconsistent.");
        }

        return acknowledgement;
    }

    private static async Task RequireCanonicalDirectoryAsync(
        IActorProxyFactory factory,
        string tenant,
        IdempotencyAdmissionDirectoryAlias[] aliases,
        string targetActorId)
    {
        IIdempotencyAdmissionDirectoryActor directory = factory
            .CreateActorProxy<IIdempotencyAdmissionDirectoryActor>(
                new ActorId(tenant),
                IdempotencyAdmissionDirectoryActor.ActorTypeName);
        IdempotencyAdmissionDirectoryResult result = await directory.ResolveAsync(
            new IdempotencyAdmissionDirectoryRequest(
                IdempotencyAdmissionDirectoryEntry.CurrentSchemaVersion,
                targetActorId,
                aliases,
                targetActorId)).ConfigureAwait(false);
        if (result.PromotionPhase != IdempotencyAdmissionPromotionPhase.Stable
            || !string.Equals(result.CanonicalActorId, targetActorId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Legacy migration directory authority is inconsistent.");
        }
    }

    private static async Task<string?> ReproveCurrentAuthorityAsync(
        IActorProxyFactory factory,
        string tenant,
        IdempotencyAdmissionDirectoryAlias[] aliases,
        string pinnedTargetActorId)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        string currentActorId = pinnedTargetActorId;
        string previousActorId = pinnedTargetActorId;
        for (int hop = 0; hop < aliases.Length; hop++)
        {
            if (!visited.Add(currentActorId))
            {
                return null;
            }

            IIdempotencyAdmissionActor actor = factory.CreateActorProxy<IIdempotencyAdmissionActor>(
                new ActorId(currentActorId),
                IdempotencyAdmissionActor.ActorTypeName);
            IdempotencyAdmissionInspection? inspection = await actor.InspectAsync().ConfigureAwait(false);
            if (inspection is null || !inspection.Exists)
            {
                return null;
            }

            if (!string.Equals(currentActorId, pinnedTargetActorId, StringComparison.Ordinal))
            {
                IdempotencyAdmissionPromotionAcknowledgement? promotion = inspection.Promotion;
                string conventionalMigrationId = IdempotencyAdmissionPromotionEvidence
                    .BuildConventionalMigrationId(previousActorId, currentActorId);
                if (promotion is null
                    || !promotion.Activated
                    || !string.Equals(promotion.SourceActorId, previousActorId, StringComparison.Ordinal)
                    || !string.Equals(promotion.MigrationId, conventionalMigrationId, StringComparison.Ordinal)
                    || !string.Equals(
                        promotion.SourceEvidenceDigest,
                        promotion.ImportDigest,
                        StringComparison.Ordinal))
                {
                    return null;
                }
            }

            if (inspection.RedirectActorId is null)
            {
                IIdempotencyAdmissionDirectoryInspectionActor directory = factory
                    .CreateActorProxy<IIdempotencyAdmissionDirectoryInspectionActor>(
                        new ActorId(tenant),
                        IdempotencyAdmissionDirectoryActor.ActorTypeName);
                IdempotencyAdmissionDirectoryResult? result = await directory
                    .InspectAsync(aliases).ConfigureAwait(false);
                return result is not null
                    && result.PromotionPhase == IdempotencyAdmissionPromotionPhase.Stable
                    && string.Equals(result.CanonicalActorId, currentActorId, StringComparison.Ordinal)
                    ? currentActorId
                    : null;
            }

            previousActorId = currentActorId;
            currentActorId = inspection.RedirectActorId;
            if (!aliases.Any(alias => string.Equals(
                alias.ActorId,
                currentActorId,
                StringComparison.Ordinal)))
            {
                return null;
            }
        }

        return null;
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

    private static DateTimeOffset Max(DateTimeOffset left, DateTimeOffset right)
        => left >= right ? left : right;
}
