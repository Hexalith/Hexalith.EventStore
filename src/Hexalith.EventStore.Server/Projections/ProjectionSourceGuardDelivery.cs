using System.Text.Json;

using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Events;

using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>Performs the approved double-sampled current decision without history, writes or cleanup.</summary>
internal sealed partial class ProjectionSourceGuardDelivery(INamedProjectionRouteCatalog catalogs,
    IProjectionDeliveryStateStore stateStore, IProjectionLifecycleGateway lifecycle,
    IProjectionSelectedOwnerProofGateway ownerProof, IActorProxyFactory actors, IOptions<EventStoreActorOptions> actorOptions,
    ProjectionSourceGuardFinalizer? finalizer = null, IProjectionDeliveryRetryScheduler? scheduler = null,
    IOptions<ProjectionDispatchOptions>? dispatchOptions = null, TimeProvider? timeProvider = null,
    ProjectionSourceGuardTailDelivery? tailDelivery = null, IProjectionSourceGuardTransport? transport = null,
    INamedProjectionDispatchCoordinator? compatibleDelivery = null, ProjectionSourceGuardHistoryReader? compatibleHistory = null)
    : IProjectionSourceGuardDelivery
{
    /// <inheritdoc/>
    public async Task<bool> ReclaimCompletedReplayAsync(AggregateIdentity identity, CancellationToken cancellationToken)
        => await ReclaimCompletedReplayCoreAsync(identity, null, null, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc/>
    public async Task<bool> ReclaimCompletedReplayAsync(AggregateIdentity identity, ProjectionDeliveryRetryWorkItem original,
        IReadOnlyList<string> routes, CancellationToken cancellationToken)
        => await ReclaimCompletedReplayCoreAsync(identity, original, routes, cancellationToken).ConfigureAwait(false);

    private async Task<bool> ReclaimCompletedReplayCoreAsync(AggregateIdentity identity, ProjectionDeliveryRetryWorkItem? original,
        IReadOnlyList<string>? requestedRoutes, CancellationToken cancellationToken)
    {
        NamedProjectionRouteCatalogEntry[] entries = [.. catalogs.Current.Entries.Where(entry => entry.Domain == identity.Domain
            && entry.SourceGuardCapabilities.Count > 0)];
        if (entries.Length != 1) return false;
        NamedProjectionRouteCatalogEntry catalog = entries[0];
        if (original is not null && (original.TenantId != identity.TenantId || original.Domain != identity.Domain
            || original.AggregateId != identity.AggregateId || original.AppId != catalog.AppId || original.ServiceVersion != catalog.ServiceVersion
            || original.CatalogFingerprint != catalog.CatalogFingerprint || original.HeadMessageId != original.DispatchId
            || original.WorkId != ProjectionDeliveryRetryWorkItem.CreateWorkId(identity.TenantId, identity.Domain, identity.AggregateId, original.HeadSequence)
            || requestedRoutes is not { Count: > 0 } || requestedRoutes.Distinct(StringComparer.Ordinal).Count() != requestedRoutes.Count
            || requestedRoutes.Any(route => !original.PendingRoutes.Contains(route, StringComparer.Ordinal)
                || original.TerminalRoutes.Contains(route, StringComparer.Ordinal)))) return false;
        IAggregateActor source = actors.CreateActorProxy<IAggregateActor>(new ActorId(identity.ActorId), actorOptions.Value.AggregateActorTypeName);
        var owners = new List<ProjectionOwnerProof>();
        foreach (string projection in requestedRoutes ?? catalog.OrdinaryProjectionTypes)
        {
            if (!catalog.OrdinaryProjectionTypes.Contains(projection, StringComparer.Ordinal)) return false;
            SourceProofInspection first = await source.InspectSourceProofAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
            ProjectionDeliveryStateReadResult read = await stateStore.ReadAsync(identity, projection, cancellationToken).ConfigureAwait(false);
            if (original is null && read.SourceGuard is null && read.State?.SchemaVersion != ProjectionDeliveryState.GuardSchemaVersion) continue;
            if (read.Classification != ProjectionDeliveryStateClassification.Current || !read.WriterProtocolV2Active
                || read.SourceGuard is not { } marker || read.State is not { ActiveReservation: null, IdleBindingPending: null } row
                || !ValidCurrentRow(row, identity, projection) || row.GuardEvidence is not { } binding
                || binding.CatalogFingerprint != catalog.CatalogFingerprint
                || !catalog.SourceGuardCapabilities.TryGetValue(projection, out ProjectionSourceGuardCapability? capability)
                || !capability.IsSupported || binding.HandlerSemantics != capability.HandlerSemantics
                || !marker.IsValid(identity, projection) || marker.CatalogFingerprint != catalog.CatalogFingerprint
                || marker.HandlerSemantics != capability.HandlerSemantics || marker.SourceIncarnation != binding.Source.SourceIncarnation
                || first.Status != SourceProofInspectionStatus.Available || first.Erased || first.Proof is not { } sourceProof
                || sourceProof.SourceIncarnation != binding.Source.SourceIncarnation || sourceProof.HeadSequence < binding.Source.HeadSequence
                || !SourceProofCodec.IsValid(sourceProof, identity, sourceProof.HeadSequence, sourceProof.RetainedFloor)
                || sourceProof.HeadSequence == binding.Source.HeadSequence && !SourceProofCodec.SameCompletedPrefix(binding.Source, sourceProof)
                || original is not null && !CompletedOriginal(row, original)) return false;
            ProjectionLifecycleSnapshot idle = await lifecycle.ReadSnapshotAsync(identity, projection, cancellationToken).ConfigureAwait(false);
            if (idle.Phase != ProjectionLifecyclePhase.Idle || idle.Revision != binding.IdleLifecycleRevision) return false;
            ProjectionOwnerProofResult selected = await ownerProof.ReadAsync(identity, catalog, projection, idle, cancellationToken).ConfigureAwait(false);
            if (selected.Proof != binding.OwnerProof || !ProjectionDeliveryIdempotencyCoordinator.ValidOwnerProof(binding.OwnerProof, identity, projection)
                || await source.InspectSourceProofAsync().WaitAsync(cancellationToken).ConfigureAwait(false) != first) return false;
            if (binding.OwnerProof.Selection.ReceiptProjectionName == "rebuild") owners.Add(binding.OwnerProof);
        }
        foreach (IGrouping<string, ProjectionOwnerProof> group in owners.GroupBy(static owner => owner.Selection.BatchId, StringComparer.Ordinal))
            if (transport is null || !await transport.ReclaimReplayAsync(catalog,
                ProjectionSourceGuardReplayCleanup.CreateRequest(identity, catalog, group.ToArray()), cancellationToken).ConfigureAwait(false)) return false;
        return true;
    }

    private static bool CompletedOriginal(ProjectionDeliveryState row, ProjectionDeliveryRetryWorkItem original)
    {
        if (original.HeadSequence <= 0 || row.LastDeliveredSequence < original.HeadSequence) return false;
        if (row.LastDeliveredSequence == original.HeadSequence) return row.LastCompletedMessageId == original.HeadMessageId;
        // Only an unreserved wake-up can be superseded without original private model
        // preparation to reclaim. Sequence order alone never establishes its identity.
        if (original.ReservationFencingTokens.Count != 0 || row.CompletedMessageIdDigests is not { } ids
            || original.HeadSequence > ids.Count) return false;
        string digest = Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(original.HeadMessageId)));
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.ProofVisits);
        if (ids[checked((int)original.HeadSequence - 1)] != digest) return false;
        return original.HeadSequence < row.FirstRetainedSequence || ProjectionDeliveryCostDiagnostics.VisitProof(row.CompletedReceipts!)
            .Any(receipt => receipt.SequenceNumber == original.HeadSequence && receipt.MessageId == original.HeadMessageId);
    }

    /// <inheritdoc/>
    public async Task<ProjectionSourceGuardDeliveryResult> TryDeliverAsync(AggregateIdentity identity,
        CancellationToken cancellationToken)
        => await TryObserveAsync(identity, true, cancellationToken).ConfigureAwait(false);

    /// <summary>Runs the same owning production path under an explicitly bounded requested count profile.</summary>
    internal async Task<ProjectionSourceGuardDeliveryResult> TryDeliverAsync(AggregateIdentity identity,
        int pageSize, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageSize);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pageSize, 256);
        return await TryObserveAsync(identity, true, cancellationToken, pageSize).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<ProjectionSourceGuardDeliveryResult> TryDeliverAsync(AggregateIdentity identity,
        ProjectionDeliveryRetryWorkItem original, int pageSize, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageSize);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pageSize, 256);
        return await TryObserveAsync(identity, true, cancellationToken, pageSize, original).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<ProjectionSourceGuardDeliveryResult> InspectCurrentAsync(AggregateIdentity identity,
        CancellationToken cancellationToken)
        => await TryObserveAsync(identity, false, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc/>
    public async Task<ProjectionSourceGuardDeliveryResult> InspectCurrentAsync(AggregateIdentity identity, string projection,
        CancellationToken cancellationToken)
    {
        if (catalogs.Current.Entries.Any(entry => entry.Domain == identity.Domain
            && entry.OrdinaryProjectionTypes.Contains(projection, StringComparer.Ordinal)))
            return await InspectCurrentAsync(identity, cancellationToken).ConfigureAwait(false);
        try
        {
            ProjectionDeliveryStateReadResult removed = await stateStore.ReadAsync(identity, projection, cancellationToken).ConfigureAwait(false);
            return removed.SourceGuard is not null || removed.State?.SchemaVersion == ProjectionDeliveryState.GuardSchemaVersion
                ? new(true, false, "guard_activation_required") : new(false, false, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { return new(true, false, "delivery_state_unavailable"); }
    }

    private async Task<ProjectionSourceGuardDeliveryResult> TryObserveAsync(AggregateIdentity identity,
        bool allowDelivery, CancellationToken cancellationToken, int pageSize = 1, ProjectionDeliveryRetryWorkItem? originalWork = null)
    {
        var telemetry = new ProjectionSourceGuardAttemptTelemetry();
        ProjectionSourceGuardDeliveryResult result;
        try { result = await TryDeliverCoreAsync(identity, allowDelivery, cancellationToken, pageSize, originalWork, telemetry).ConfigureAwait(false); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        { telemetry.Record(false, "delivery_reconciliation_required"); throw; }
        catch (OperationCanceledException) { result = new(true, false,
            allowDelivery ? "delivery_reconciliation_required" : "delivery_state_unavailable"); }
        catch (ProjectionDeliveryHistoryValidationException exception)
        {
            string reason = exception.Message switch
            {
                "delivery_identity_conflict" or "delivery_gap" or "history_unavailable" or "source_conflict"
                    or "delivery_page_limit_exceeded" or "delivery_byte_limit_exceeded" or "delivery_owner_limit_exceeded"
                    or "delivery_proof_limit_exceeded" or "delivery_state_unavailable" => exception.Message,
                _ => "source_conflict",
            };
            result = new(true, false, reason);
        }
        catch { result = new(true, false, "delivery_state_unavailable"); }
        if (result.Owned) telemetry.Record(result.AlreadyCurrent, result.ReasonCode);
        return result with { FinalizationStartedTimestamp = telemetry.FinalizationStartedTimestamp };
    }

    private async Task<ProjectionSourceGuardDeliveryResult> TryDeliverCoreAsync(AggregateIdentity identity,
        bool allowDelivery, CancellationToken cancellationToken, int pageSize, ProjectionDeliveryRetryWorkItem? originalWork,
        ProjectionSourceGuardAttemptTelemetry telemetry)
    {
        NamedProjectionRouteCatalogEntry[] entries = [.. catalogs.Current.Entries.Where(entry => entry.Domain == identity.Domain
            && entry.OrdinaryProjectionTypes.Count > 0)];
        if (entries.Length == 0) return new(false, false, null);
        if (entries.Length != 1) return new(true, false, "delivery_reconciliation_required");
        NamedProjectionRouteCatalogEntry entry = entries[0];
        foreach (string route in entry.OrdinaryProjectionTypes) telemetry.Add(route);
        IAggregateActor source = actors.CreateActorProxy<IAggregateActor>(new ActorId(identity.ActorId), actorOptions.Value.AggregateActorTypeName);
        SourceProofInspection? observedSource = null;
        bool emptyNoWork = false;
        bool owned = false;
        string? incompleteReason = null;
        var freshness = new Dictionary<string, string>(StringComparer.Ordinal);
        var owners = new Dictionary<string, ProjectionOwnerProof>(StringComparer.Ordinal);
        var tails = new List<ProjectionSourceGuardRouteObservation>();
        var completedSiblings = new List<ProjectionSourceGuardRouteObservation>();
        var compatibleRoutes = new List<string>();
        foreach (string projection in entry.OrdinaryProjectionTypes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SourceProofInspection? firstSource = null;
            Exception? sourceFailure = null;
            if (entry.SourceGuardCapabilities.ContainsKey(projection))
            {
                try
                {
                    firstSource = await source.InspectSourceProofAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
                    telemetry.Head(projection, firstSource.CurrentSequence);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                catch (Exception exception) { sourceFailure = exception; }
            }
            ProjectionDeliveryStateReadResult read;
            try { read = await stateStore.ReadAsync(identity, projection, cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException) { throw; }
            catch (ProjectionDeliveryHistoryValidationException exception)
            {
                owned = true;
                incompleteReason ??= exception.Message == "delivery_proof_limit_exceeded"
                    ? exception.Message : "delivery_reconciliation_required";
                continue;
            }
            catch (JsonException)
            {
                owned = true;
                incompleteReason ??= "guard_activation_required";
                continue;
            }
            catch
            {
                owned = true;
                incompleteReason ??= "delivery_state_unavailable";
                continue;
            }
            bool observedRowValid = read.Classification == ProjectionDeliveryStateClassification.Current && read.State is { } observedRow
                && ValidCurrentRow(observedRow, identity, projection);
            if (observedRowValid) telemetry.Checkpoint(projection, read.State!.LastDeliveredSequence);
            if (read.SourceGuard is null)
            {
                owned |= read.State?.SchemaVersion == ProjectionDeliveryState.GuardSchemaVersion;
                if (read.State?.SchemaVersion != ProjectionDeliveryState.GuardSchemaVersion) compatibleRoutes.Add(projection);
                incompleteReason ??= "guard_activation_required";
                continue;
            }
            owned = true;
            ProjectionSourceGuard marker = read.SourceGuard;
            if (!entry.SourceGuardCapabilities.TryGetValue(projection, out ProjectionSourceGuardCapability? capability))
            {
                incompleteReason ??= "unsupported_capability";
                continue;
            }
            if (!marker.IsValid(identity, projection) || !capability.IsSupported
                || marker.CatalogFingerprint != entry.CatalogFingerprint || marker.HandlerSemantics != capability.HandlerSemantics
                || !read.WriterProtocolV2Active)
            {
                incompleteReason ??= "guard_activation_required";
                continue;
            }
            // Verified guarded catalogs preserve S1,row+markers,lifecycle,
            // owner,S2 ordering. Descriptor-absent guard-off routes never call
            // additive actor APIs; an unbound rolling actor remains compatible.
            if (sourceFailure is not null) throw sourceFailure;
            SourceProofInspection first = firstSource ?? throw new InvalidOperationException("delivery_state_unavailable");
            if (observedSource is not null && observedSource != first) return new(true, false, "head_changed");
            observedSource = first;
            ProjectionLifecycleSnapshot before = await lifecycle.ReadSnapshotAsync(identity, projection, cancellationToken).ConfigureAwait(false);
            ProjectionOwnerProofResult selected = await ownerProof.ReadAsync(identity, entry, projection, before, cancellationToken).ConfigureAwait(false);
            // Tail admission authenticates the complete frozen prefix. Keep its remaining
            // source samples around the completion CAS; Current still needs two samples here.
            bool actualTail = allowDelivery && observedRowValid && read.State is { } observed
                && observed.LastDeliveredSequence < first.CurrentSequence
                && first.Status == SourceProofInspectionStatus.Available && first.Proof is not null && !first.Erased;
            if (!actualTail)
            {
                SourceProofInspection second = await source.InspectSourceProofAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
                if (first != second) return new(true, false, "head_changed");
            }
            if (first.Erased)
                return new(true, false, "delivery_in_progress");
            ProjectionDeliveryState? state = read.State;
            if (first.CurrentSequence == 0)
            {
                if (state?.LastDeliveredSequence is null or 0 && state?.GuardEvidence is null
                    && before.Phase == ProjectionLifecyclePhase.Idle
                    && state?.ActiveReservation is null && selected.Proof is null && selected.ReasonCode == "delivery_rebuild_required"
                    && state?.IdleBindingPending is null)
                {
                    emptyNoWork = true;
                    continue;
                }

                return new(true, false, "checkpoint_drift");
            }
            if (first.Status != SourceProofInspectionStatus.Available || first.Proof is not { } sourceProof)
                return new(true, false, first.Status == SourceProofInspectionStatus.Unsupported ? "source_proof_missing" : "source_conflict");
            if (sourceProof.HeadSequence > 10_000) return new(true, false, "delivery_owner_limit_exceeded");
            if (marker.SourceIncarnation != sourceProof.SourceIncarnation) return new(true, false, "source_conflict");
            if (state is null || state.SchemaVersion != ProjectionDeliveryState.GuardSchemaVersion || state.GuardEvidenceVersion != 1
                || !observedRowValid)
            {
                // An invalid route never owns a sibling's delivery fence. Keep its evidence
                // untouched while an independently qualified sibling may make progress.
                incompleteReason ??= state is not null && state.SchemaVersion != ProjectionDeliveryState.GuardSchemaVersion
                    ? "delivery_schema_regression" : "delivery_reconciliation_required";
                continue;
            }
            if (state.IdleBindingPending is not null)
                return allowDelivery
                    ? await RecoverPendingAsync(identity, entry, projection, state, pageSize, cancellationToken, telemetry).ConfigureAwait(false)
                    : new(true, false, "delivery_in_progress");
            if (state.ActiveReservation is { GuardBinding: null }
                || state.ActiveReservation is null && before.Phase != ProjectionLifecyclePhase.Idle)
                return new(true, false, "delivery_in_progress");
            if (state.LastDeliveredSequence > sourceProof.HeadSequence) return new(true, false, "checkpoint_drift");
            if (state.LastDeliveredSequence < sourceProof.HeadSequence)
            {
                telemetry.Mode(projection, capability.HandlerSemantics == "full-replay-v1" ? "full_replay" : "tail");
                tails.Add(new(projection, state, before, selected));
                continue;
            }
            if (state.ActiveReservation is not null) return new(true, false, "delivery_in_progress");
            ProjectionDeliveryGuardEvidence? evidence = state.GuardEvidence;
            if (evidence is null || selected.Proof is null || !SourceProofCodec.SameCompletedPrefix(evidence.Source, sourceProof)
                || !ProjectionDeliveryIdempotencyCoordinator.ValidOwnerProof(selected.Proof, identity, projection)
                || selected.Proof.Selection.AppliedSequence != state.LastDeliveredSequence
                || selected.Proof.Selection.MessageId != state.LastCompletedMessageId
                || marker.SourceIncarnation != sourceProof.SourceIncarnation
                || state.LastCompletedMessageId != sourceProof.HeadMessageId
                || evidence.CatalogFingerprint != entry.CatalogFingerprint || evidence.HandlerSemantics != capability.HandlerSemantics
                || evidence.OwnerProof != selected.Proof || evidence.IdleLifecycleRevision != before.Revision)
                return new(true, false, selected.ReasonCode ?? "delivery_reconciliation_required");
            freshness[projection] = selected.Proof.Selection.FreshnessVersion;
            owners[projection] = selected.Proof;
            completedSiblings.Add(new(projection, state, before, selected));
            telemetry.Terminal(projection, "current", "already_completed", "delivery_already_completed",
                sourceProof.HeadSequence, state.LastDeliveredSequence);
        }

        if (!owned) return new(false, false, null);
        // Retry-only settlement may leave unrelated compatible work pending.
        // A current sibling must never short-circuit unfinished guarded tails.
        if (tails.Count == 0 && originalWork is not null && observedSource?.Proof is { } retryHead
            && originalWork.TenantId == identity.TenantId && originalWork.Domain == identity.Domain
            && originalWork.AggregateId == identity.AggregateId && originalWork.CatalogFingerprint == entry.CatalogFingerprint
            && originalWork.HeadSequence == retryHead.HeadSequence && originalWork.HeadMessageId == retryHead.HeadMessageId
            && originalWork.PendingRoutes.Any(freshness.ContainsKey))
            return new(true, !emptyNoWork && incompleteReason is null, incompleteReason)
            {
                ObservedHeadSequence = retryHead.HeadSequence, ObservedHeadMessageId = retryHead.HeadMessageId,
                CatalogFingerprint = entry.CatalogFingerprint, ProvenFreshnessVersions = freshness,
            };
        if (tails.Count > 0)
        {
            if (!allowDelivery) return new(true, false, incompleteReason ?? "head_changed");
            ProjectionSourceGuardDeliveryResult delivered = tailDelivery is null || observedSource?.Proof is not { } head
                ? new(true, false, "delivery_rebuild_required")
                : await tailDelivery.DeliverAsync(identity, entry, head, tails.Concat(completedSiblings).ToArray(), pageSize, cancellationToken, telemetry).ConfigureAwait(false);
            if (compatibleRoutes.Count > 0 && observedSource?.Proof is { } compatibleHead)
                await DeliverCompatibleAsync(identity, entry, compatibleHead, compatibleRoutes, cancellationToken).ConfigureAwait(false);
            return incompleteReason is null ? delivered : delivered with { AlreadyCurrent = false, ReasonCode = incompleteReason };
        }
        if (allowDelivery && compatibleRoutes.Count > 0 && observedSource?.Proof is { HeadSequence: > 0 } currentHead)
            await DeliverCompatibleAsync(identity, entry, currentHead, compatibleRoutes, cancellationToken).ConfigureAwait(false);
        return new(true, !emptyNoWork && incompleteReason is null, incompleteReason ?? (emptyNoWork ? "empty_no_work" : null))
        {
            ObservedHeadSequence = observedSource?.Proof?.HeadSequence,
            ObservedHeadMessageId = observedSource?.Proof?.HeadMessageId,
            CatalogFingerprint = entry.CatalogFingerprint,
            ProvenFreshnessVersions = freshness,
            ProvenOwners = owners,
            ProvenSource = observedSource?.Proof,
        };
    }

    private async Task DeliverCompatibleAsync(AggregateIdentity identity, NamedProjectionRouteCatalogEntry catalog,
        Events.SourceProof head, IReadOnlyList<string> routes, CancellationToken cancellationToken)
    {
        if (compatibleDelivery is null || compatibleHistory is null) return;
        var events = new List<ProjectionEventDto>();
        await foreach (ProjectionSourceGuardPage page in compatibleHistory.ReadPagesAsync(identity,
            Events.SourceProofCodec.CreateSeed(identity, head.SourceIncarnation), head, 1, cancellationToken).ConfigureAwait(false))
            events.AddRange(page.Readable);
        _ = await compatibleDelivery.TryDispatchCompatibleAsync(identity, catalog, head.HeadSequence, head.HeadMessageId!,
            [.. events], routes, cancellationToken).ConfigureAwait(false);
    }

    private async Task<ProjectionSourceGuardDeliveryResult> RecoverPendingAsync(AggregateIdentity identity,
        NamedProjectionRouteCatalogEntry entry, string projection, ProjectionDeliveryState state, int pageSize, CancellationToken cancellationToken,
        ProjectionSourceGuardAttemptTelemetry telemetry)
    {
        if (finalizer is null || scheduler is null || state.ActiveReservation is not { GuardBinding: { } binding } reservation
            || binding.CatalogFingerprint != entry.CatalogFingerprint
            || binding.HandlerSemantics != entry.SourceGuardCapabilities[projection].HandlerSemantics)
            return new(true, false, "delivery_reconciliation_required");
        ProjectionDispatchOptions options = dispatchOptions?.Value ?? new();
        options.Validate();
        DateTimeOffset now = (timeProvider ?? TimeProvider.System).GetUtcNow();
        var proposed = new ProjectionDeliveryRetryWorkItem(ProjectionDeliveryRetryWorkItem.CreateWorkId(identity.TenantId,
            identity.Domain, identity.AggregateId, reservation.EndSequence), identity.TenantId, identity.Domain,
            identity.AggregateId, entry.AppId, entry.ServiceVersion, reservation.EndSequence, reservation.HeadMessageId,
            [.. entry.OrdinaryProjectionTypes], [], reservation.DispatchId, entry.CatalogFingerprint, 0,
            now + options.RetryWorkerInterval, null) { ReplayRecoveryBindings = ProjectionReplayRecoveryContext.Freeze(entry) };
        ProjectionDeliveryRetryWorkItem scheduled = await scheduler.ScheduleAsync(proposed, cancellationToken).ConfigureAwait(false);
        if (scheduled.HeadSequence != reservation.EndSequence || scheduled.DispatchId != reservation.DispatchId
            || scheduled.CatalogFingerprint != entry.CatalogFingerprint || scheduled.AppId != entry.AppId
            || scheduled.ServiceVersion != entry.ServiceVersion
            || !ProjectionReplayRecoveryContext.Matches(scheduled.ReplayRecoveryBindings, proposed.ReplayRecoveryBindings))
            return new(true, false, "delivery_reconciliation_required");
        ProjectionDeliveryRetryWorkItem? claimed = await scheduler.TryAcquireAsync(scheduled,
            Guid.NewGuid().ToString("N", System.Globalization.CultureInfo.InvariantCulture), now,
            options.RetryLeaseDuration, cancellationToken).ConfigureAwait(false);
        if (claimed is null) return new(true, false, "delivery_in_progress");
        long? token = claimed.ReservationFencingTokens.TryGetValue(projection, out long fence) ? fence : null;
        TimeProvider clock = timeProvider ?? TimeProvider.System;
        using var finalizationBudget = new ProjectionSourceGuardFinalizationBudget(clock, () => telemetry.StartFinalization(clock));
        try
        {
            ProjectionSourceGuardDeliveryResult recovered = await finalizer.RecoverAsync(identity, entry, projection,
                state, pageSize, cancellationToken, token, finalizationBudget).ConfigureAwait(false);
            return recovered with { AlreadyCurrent = false,
                ReasonCode = recovered.AlreadyCurrent ? "delivery_already_completed" : recovered.ReasonCode };
        }
        finally
        {
            int attempt = claimed.Attempt < options.MaxRetryAttempts ? claimed.Attempt + 1 : options.MaxRetryAttempts;
            await scheduler.TryUpdateAsync(claimed with { Attempt = attempt,
                NextDueUtc = clock.GetUtcNow() + ProjectionSourceGuardTailDelivery.GetRetryDelay(attempt, options) }, finalizationBudget.Begin()).ConfigureAwait(false);
        }
    }

    private static bool ValidCurrentRow(ProjectionDeliveryState state, AggregateIdentity identity, string projection)
    {
        string? fingerprint = state.CompletedPrefixFingerprint;
        return fingerprint is not null && ProjectionDeliveryIdempotencyCoordinator.IsValidCurrentState(state, identity, projection,
                ProjectionDeliveryFingerprint.ComputeInitial(identity, projection, fingerprint.StartsWith("v2:", StringComparison.Ordinal)))
            && ProjectionDeliveryIdempotencyCoordinator.ValidCompleteIdentityDigests(state);
    }
}
