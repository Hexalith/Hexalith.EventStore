using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Events;

using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>Admits frozen readable tails, shares application pages, and resumes original per-event receipts.</summary>
internal sealed partial class ProjectionSourceGuardTailDelivery(ProjectionSourceGuardHistoryReader history,
    IProjectionDeliveryIdempotencyCoordinator coordinator, IProjectionDeliveryRetryScheduler scheduler,
    IProjectionLifecycleGateway lifecycle, IProjectionSelectedOwnerProofGateway owners,
    IProjectionSourceGuardTransport transport, ProjectionSourceGuardFinalizer finalizer,
    IOptions<ProjectionDispatchOptions> dispatchOptions, TimeProvider clock)
{
    /// <summary>Delivers explicitly negotiated per-event routes; no handler receives a page as a stream.</summary>
    internal async Task<ProjectionSourceGuardDeliveryResult> DeliverAsync(AggregateIdentity identity,
        NamedProjectionRouteCatalogEntry catalog, SourceProof observedHead,
        IReadOnlyList<ProjectionSourceGuardRouteObservation> observations, int pageSize, CancellationToken cancellationToken,
        ProjectionSourceGuardAttemptTelemetry? telemetry = null)
    {
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.Activations);
        var plans = new List<ProjectionSourceGuardTailPlan>();
        ProjectionEventDto[]? sharedReplayEvents = null;
        SourceProof[] pendingHeads = [.. observations.Where(static item => item.State.ActiveReservation is not null)
            .Select(static item => item.State.ActiveReservation!.GuardBinding?.AdmittedSource).OfType<SourceProof>().Distinct()];
        if (pendingHeads.Length > 1) return new(true, false, "delivery_in_progress");
        SourceProof head = pendingHeads.SingleOrDefault() ?? observedHead;
        if (head.SourceIncarnation != observedHead.SourceIncarnation || head.HeadSequence > observedHead.HeadSequence
            || observedHead.RetainedFloor - 1 > observations.Min(static item => item.State.LastDeliveredSequence))
            return new(true, false, "source_conflict");
        // A completed row can still own original retry/private cleanup. Keep that
        // selected generation intact until its exact old work has been reclaimed;
        // a later head must never make the old reserved completion unverifiable.
        foreach (long completed in observations.Where(static item => item.State.ActiveReservation is null
            && item.State.LastDeliveredSequence > 0 && item.State.LastDeliveredSequence == item.State.GuardEvidence?.Source.HeadSequence)
            .Select(static item => item.State.LastDeliveredSequence).Where(value => value < head.HeadSequence).Distinct())
        {
            string originalId = ProjectionDeliveryRetryWorkItem.CreateWorkId(identity.TenantId, identity.Domain, identity.AggregateId, completed);
            if (await scheduler.ReadAsync(originalId, cancellationToken).ConfigureAwait(false) is { } retained
                && (retained.ReservationFencingTokens.Count > 0 || observations.Any(item => item.State.LastDeliveredSequence == completed
                    && item.State.GuardEvidence?.OwnerProof.Selection is { ReceiptProjectionName: "rebuild" } selection
                    && selection.BatchId == retained.DispatchId)))
            {
                var completedOwners = observations.Where(item => item.State.ActiveReservation is null
                    && item.State.LastDeliveredSequence == retained.HeadSequence && item.State.LastCompletedMessageId == retained.HeadMessageId
                    && item.State.GuardEvidence is { } binding && binding.CatalogFingerprint == catalog.CatalogFingerprint
                    && binding.OwnerProof == item.Selected.Proof && item.Lifecycle.Phase == ProjectionLifecyclePhase.Idle
                    && item.Lifecycle.Revision == binding.IdleLifecycleRevision)
                    .ToDictionary(static item => item.Projection, static item => item.Selected.Proof!.Selection.FreshnessVersion, StringComparer.Ordinal);
                return new(true, false, "delivery_in_progress") { ObservedHeadSequence = observedHead.HeadSequence,
                    ObservedHeadMessageId = observedHead.HeadMessageId, CatalogFingerprint = catalog.CatalogFingerprint,
                    ProvenFreshnessVersions = completedOwners };
            }
        }
        var completedReplayOwners = observations.Where(item => item.State.ActiveReservation is null
            && item.State.LastDeliveredSequence == head.HeadSequence && item.State.GuardEvidence is { } binding
            && binding.CatalogFingerprint == catalog.CatalogFingerprint && binding.Source == head
            && binding.OwnerProof == item.Selected.Proof && item.Lifecycle.Phase == ProjectionLifecyclePhase.Idle
            && item.Lifecycle.Revision == binding.IdleLifecycleRevision && binding.OwnerProof.Selection.BatchId == head.HeadMessageId
            && binding.OwnerProof.Selection.ReceiptProjectionName == "rebuild")
            .Select(static item => item.Selected.Proof!).ToArray();
        foreach (ProjectionSourceGuardRouteObservation item in observations)
        {
            string route = item.Projection;
            ProjectionDeliveryState state = item.State;
            if (state.LastDeliveredSequence >= head.HeadSequence) continue;
            ProjectionSourceGuardCapability capability = catalog.SourceGuardCapabilities[route];
            if (!SupportsRoute(catalog, route, capability))
                return new(true, false, "delivery_rebuild_required");
            bool canonicalReplay = capability.HandlerSemantics == "full-replay-v1"
                || state.ActiveReservation?.GuardBinding?.CanonicalReplay == true
                || state.LastDeliveredSequence > 0 && state.ActiveReservation is null
                    && capability.HandlerSemantics is "mapped-incremental-replay-v1" or "single-event-v1"
                    && item.Selected.Proof is null && SupportsCanonicalReplay(catalog, route);
            if (state.LastDeliveredSequence == 0)
            {
                if (capability.InitialOwnerProtocol != "aggregate-first-event/v1" || state.GuardEvidence is not null
                    || state.ActiveReservation is null && (item.Selected.Proof is not null
                        || item.Selected.ReasonCode != "delivery_rebuild_required" || item.Lifecycle.Phase != ProjectionLifecyclePhase.Idle))
                    return new(true, false, "delivery_reconciliation_required");
                SourceProof seed = SourceProofCodec.CreateSeed(identity, head.SourceIncarnation);
                ProjectionSourceGuardReplayAdmission? initialReplay = canonicalReplay
                    ? await history.AdmitReplayAsync(identity, route, state, head, pageSize, true, cancellationToken,
                        sharedReplayEvents, true).ConfigureAwait(false) : null;
                sharedReplayEvents ??= initialReplay?.Events;
                ProjectionSourceAdmittedHistory initial = initialReplay?.Tail ?? await history.AdmitAsync(identity, route, seed,
                    head, state.CompletedPrefixFingerprint!, pageSize, cancellationToken).ConfigureAwait(false);
                var fresh = new ProjectionDeliveryGuardReservation(seed, head, catalog.CatalogFingerprint,
                    capability.HandlerSemantics, initial.Events[0].MessageId, null) { CanonicalReplay = canonicalReplay };
                if (state.ActiveReservation is { } previous && (previous.GuardBinding != fresh
                    || previous.ManifestFingerprint != initial.History.PrefixFingerprint))
                    return new(true, false, "delivery_reconciliation_required");
                plans.Add(new(route, state, fresh, initial, item.Selected.Proof) { ReplayEvents = initialReplay?.Events });
                continue;
            }
            ProjectionDeliveryGuardEvidence? completed = state.GuardEvidence;
            ProjectionOwnerProof? selected = item.Selected.Proof ?? (state.ActiveReservation is not null || canonicalReplay ? completed?.OwnerProof : null);
            if (completed is null || selected is null
                || !ProjectionDeliveryIdempotencyCoordinator.ValidOwnerProof(selected, identity, route)
                || completed.Source.HeadSequence != state.LastDeliveredSequence
                || completed.Source.HeadMessageId != state.LastCompletedMessageId
                || completed.CatalogFingerprint != catalog.CatalogFingerprint || completed.HandlerSemantics != capability.HandlerSemantics
                || completed.Source.SourceIncarnation != head.SourceIncarnation)
                return new(true, false, item.Selected.ReasonCode ?? "delivery_reconciliation_required");
            ProjectionDeliveryGuardReservation binding;
            if (state.ActiveReservation is { } active)
            {
                if (active.GuardBinding is not { } original || original.AdmittedSource != head
                    || original.BaseSource != completed.Source || original.BaseOwnerProof != completed.OwnerProof
                    || original.CatalogFingerprint != catalog.CatalogFingerprint || original.HandlerSemantics != capability.HandlerSemantics)
                    return new(true, false, "delivery_reconciliation_required");
                binding = original;
            }
            else
            {
                if (selected != completed.OwnerProof || item.Lifecycle.Phase != ProjectionLifecyclePhase.Idle
                    || item.Lifecycle.Revision != completed.IdleLifecycleRevision)
                    return new(true, false, "delivery_reconciliation_required");
                binding = new(completed.Source, head, catalog.CatalogFingerprint, capability.HandlerSemantics,
                    selected.Selection.OwnerGeneration, selected) { CanonicalReplay = canonicalReplay };
            }
            if (selected.Selection.OwnerGeneration != binding.SelectedOwnerGeneration
                || selected.Selection.OwnerIdentity != completed.OwnerProof.Selection.OwnerIdentity
                || selected.Selection.AppliedSequence < state.LastDeliveredSequence || selected.Selection.AppliedSequence > head.HeadSequence)
                return new(true, false, "delivery_reconciliation_required");
            ProjectionSourceGuardReplayAdmission? replay = canonicalReplay
                ? await history.AdmitReplayAsync(identity, route, state, head, pageSize, true, cancellationToken,
                    sharedReplayEvents, true).ConfigureAwait(false) : null;
            sharedReplayEvents ??= replay?.Events;
            ProjectionSourceAdmittedHistory admitted = replay?.Tail ?? await history.AdmitAsync(identity, route, binding.BaseSource,
                head, state.CompletedPrefixFingerprint!, pageSize, cancellationToken).ConfigureAwait(false);
            if (state.ActiveReservation is { } reserved && reserved.ManifestFingerprint != admitted.History.PrefixFingerprint)
                return new(true, false, "source_conflict");
            plans.Add(new(route, state, binding, admitted, selected) { ReplayEvents = replay?.Events });
        }
        if (plans.Count == 0) return new(true, false, "delivery_in_progress");

        ProjectionDispatchOptions options = dispatchOptions.Value;
        options.Validate();
        DateTimeOffset now = clock.GetUtcNow();
        var proposed = new ProjectionDeliveryRetryWorkItem(ProjectionDeliveryRetryWorkItem.CreateWorkId(identity.TenantId,
            identity.Domain, identity.AggregateId, head.HeadSequence), identity.TenantId, identity.Domain, identity.AggregateId,
            catalog.AppId, catalog.ServiceVersion, head.HeadSequence, head.HeadMessageId!, [.. catalog.OrdinaryProjectionTypes], [],
            head.HeadMessageId!, catalog.CatalogFingerprint, 0, now + options.RetryWorkerInterval, null)
            { ReplayRecoveryBindings = ProjectionReplayRecoveryContext.Freeze(catalog) };
        ProjectionDeliveryRetryWorkItem scheduled = await scheduler.ScheduleAsync(proposed, cancellationToken).ConfigureAwait(false);
        if (scheduled.HeadSequence != head.HeadSequence || scheduled.HeadMessageId != head.HeadMessageId
            || scheduled.DispatchId != head.HeadMessageId || scheduled.CatalogFingerprint != catalog.CatalogFingerprint
            || scheduled.AppId != catalog.AppId || scheduled.ServiceVersion != catalog.ServiceVersion
            || !ProjectionReplayRecoveryContext.Matches(scheduled.ReplayRecoveryBindings, proposed.ReplayRecoveryBindings))
            return new(true, false, "delivery_reconciliation_required");
        ProjectionDeliveryRetryWorkItem? work = await scheduler.TryAcquireAsync(scheduled,
            Guid.NewGuid().ToString("N", System.Globalization.CultureInfo.InvariantCulture), now,
            options.RetryLeaseDuration, cancellationToken).ConfigureAwait(false);
        if (work is null) return new(true, false, "delivery_in_progress");
        string operation = Guid.NewGuid().ToString("N", System.Globalization.CultureInfo.InvariantCulture);
        var held = new HashSet<string>(StringComparer.Ordinal);
        var reservations = new Dictionary<string, ProjectionDeliveryReservation>(StringComparer.Ordinal);
        var snapshots = new Dictionary<string, ProjectionLifecycleSnapshot>(StringComparer.Ordinal);
        var applied = new Dictionary<string, long>(StringComparer.Ordinal);
        var completedRoutes = new HashSet<string>(StringComparer.Ordinal);
        var finalObservations = new Dictionary<string, ProjectionSourceGuardDeliveryResult>(StringComparer.Ordinal);
        string reason = "delivery_reconciliation_required";
        bool replayCleanupComplete = true;
        CancellationTokenSource? finalizationBudget = null;
        try
        {
            foreach (ProjectionSourceGuardTailPlan plan in plans)
            {
                if (!await lifecycle.BeginDeliveryWriteAsync(identity, plan.Projection, operation, cancellationToken).ConfigureAwait(false))
                    return new(true, false, reason = "delivery_in_progress");
                held.Add(plan.Projection);
                long? resume = work.ReservationFencingTokens.TryGetValue(plan.Projection, out long token) ? token : null;
                ProjectionDeliveryAdmissionResult admission = await coordinator.TryAdmitHistoryAsync(identity, plan.Projection,
                    plan.Admitted.History, true, cancellationToken, resume, plan.Binding).ConfigureAwait(false);
                ProjectionDeliveryDiagnostics.RecordAdmission(plan.Projection, admission);
                if (admission.Reservation is not { } reservation || admission.Disposition != ProjectionDeliveryAdmissionDisposition.Dispatch)
                    return new(true, false, reason = admission.ReasonCode ?? "delivery_in_progress");
                reservations.Add(plan.Projection, reservation);
                ProjectionLifecycleSnapshot snapshot = await lifecycle.ReadSnapshotAsync(identity, plan.Projection, cancellationToken).ConfigureAwait(false);
                ProjectionOwnerProofResult progress = await owners.ReadAsync(identity, catalog, plan.Projection, snapshot, cancellationToken).ConfigureAwait(false);
                if (plan.ReplayEvents is null && plan.State.ActiveReservation is not null && snapshot.Phase == ProjectionLifecyclePhase.Delivering)
                    progress = await ReconcileOriginalProgressAsync(identity, catalog, plan, snapshot, cancellationToken).ConfigureAwait(false);
                ProjectionOwnerProof? selected = progress.Proof;
                bool empty = selected is null && (plan.Binding.BaseSource.HeadSequence == 0 && progress.ReasonCode == "delivery_rebuild_required"
                    || plan.ReplayEvents is not null && progress.ReasonCode is "delivery_rebuild_required" or "delivery_reconciliation_required");
                if (snapshot.Phase != ProjectionLifecyclePhase.Delivering || !empty && (selected is null
                    || !ValidProgress(identity, plan, selected, selected.Selection.AppliedSequence))
                    || plan.ReplayEvents is null && plan.State.ActiveReservation is null && selected != plan.Binding.BaseOwnerProof
                    || plan.ReplayEvents is not null && selected is not null && selected != plan.Binding.BaseOwnerProof
                        && (plan.State.ActiveReservation is null || selected.Selection.AppliedSequence != head.HeadSequence
                            || selected.Selection.BatchId != work.DispatchId || selected.Selection.ReceiptProjectionName != "rebuild"))
                    return new(true, false, reason = progress.ReasonCode ?? "delivery_reconciliation_required");
                long k = selected?.Selection.AppliedSequence ?? plan.Binding.BaseSource.HeadSequence;
                if (k < plan.Binding.BaseSource.HeadSequence || k > head.HeadSequence)
                    return new(true, false, reason = "delivery_reconciliation_required");
                foreach (ProjectionSourceAdmittedEvent item in plan.Admitted.Events.Where(item => plan.ReplayEvents is null && plan.State.ActiveReservation is null && item.Sequence <= k))
                {
                    var scope = new ProjectionOwnerProofRequest(identity.TenantId, identity.Domain, identity.AggregateId,
                        plan.Projection, catalog.CatalogFingerprint);
                    ProjectionOwnerProofResult receipt = await transport.ReadAppliedReceiptAsync(catalog,
                        new(scope, item.Sequence, item.MessageId, selected!.Selection.OwnerIdentity, selected.Selection.OwnerGeneration),
                        cancellationToken).ConfigureAwait(false);
                    if (receipt.Proof is not { } proof || !ValidProgress(identity, plan, proof, item.Sequence)
                        || proof.Selection.MessageId != item.MessageId || proof.Selection.BatchId != item.MessageId
                        || item.Sequence == k && proof != selected)
                        return new(true, false, reason = receipt.ReasonCode ?? "delivery_reconciliation_required");
                }
                snapshots.Add(plan.Projection, snapshot);
                applied.Add(plan.Projection, k);
            }

            var canonicalFallbackRoutes = new HashSet<string>(StringComparer.Ordinal);
            foreach (IGrouping<SourceProof, ProjectionSourceGuardTailPlan> group in plans.Where(static plan => plan.ReplayEvents is null)
                .GroupBy(static plan => plan.Binding.BaseSource))
            {
                await foreach (ProjectionSourceGuardPage page in history.ReadPagesAsync(identity, group.Key, head,
                    pageSize, cancellationToken).ConfigureAwait(false))
                {
                    for (int index = 0; index < page.Readable.Length; index++)
                    {
                        ProjectionEventDto item = page.Readable[index];
                        foreach (ProjectionSourceGuardTailPlan plan in group)
                        {
                            if (!Matches(plan, item, page.SourceEventDigests[index]))
                                return new(true, false, reason = "source_conflict");
                            if (canonicalFallbackRoutes.Contains(plan.Projection) || item.SequenceNumber <= applied[plan.Projection]) continue;
                            cancellationToken.ThrowIfCancellationRequested();
                            if (await lifecycle.ReadSnapshotAsync(identity, plan.Projection, cancellationToken).ConfigureAwait(false)
                                != snapshots[plan.Projection]) return new(true, false, reason = "delivery_in_progress");
                            ProjectionDispatchOutcome result = await transport.DispatchEventAsync(identity, catalog,
                                plan.Projection, item, work.DispatchId, cancellationToken).ConfigureAwait(false);
                            if (result.Status is not ProjectionDispatchStatus.Completed and not ProjectionDispatchStatus.AlreadyCompleted)
                            {
                                if (result.Status == ProjectionDispatchStatus.Retryable
                                    && result.ReasonCode == ProjectionDispatchReasonCodes.DeliveryRebuildRequired
                                    && applied[plan.Projection] == plan.Binding.BaseSource.HeadSequence
                                    && SupportsCanonicalReplay(catalog, plan.Projection))
                                {
                                    // Only an explicit no-effect result with unchanged owning progress can
                                    // choose one canonical fallback. An ambiguous response never enters here.
                                    ProjectionOwnerProofResult unchanged = await ReconcileOriginalProgressAsync(identity, catalog, plan,
                                        snapshots[plan.Projection], cancellationToken).ConfigureAwait(false);
                                    if (unchanged.Proof != plan.Binding.BaseOwnerProof || unchanged.Proof is null
                                        && unchanged.ReasonCode != ProjectionDispatchReasonCodes.DeliveryRebuildRequired)
                                        return new(true, false, reason = "delivery_reconciliation_required");
                                    canonicalFallbackRoutes.Add(plan.Projection);
                                    continue;
                                }
                                return new(true, false, reason = result.ReasonCode == ProjectionDispatchReasonCodes.DeliveryRebuildRequired
                                    ? ProjectionDispatchReasonCodes.DeliveryRebuildRequired : "handler_failure");
                            }
                            ProjectionOwnerProofResult output = await owners.ReadAsync(identity, catalog, plan.Projection,
                                snapshots[plan.Projection], cancellationToken).ConfigureAwait(false);
                            if (output.Proof is not { } selected || !ValidProgress(identity, plan, selected, item.SequenceNumber)
                                || selected.Selection.MessageId != item.MessageId || selected.Selection.BatchId != item.MessageId)
                                return new(true, false, reason = output.ReasonCode ?? "delivery_reconciliation_required");
                            applied[plan.Projection] = item.SequenceNumber;
                        }
                    }
                }
            }
            foreach (string route in canonicalFallbackRoutes)
            {
                int planIndex = plans.FindIndex(plan => plan.Projection == route);
                ProjectionSourceGuardTailPlan original = plans[planIndex];
                ProjectionSourceGuardReplayAdmission replay = await history.AdmitReplayAsync(identity, original.Projection,
                    original.State, head, pageSize, true, cancellationToken, sharedReplayEvents, true).ConfigureAwait(false);
                sharedReplayEvents ??= replay.Events;
                ProjectionDeliveryReservation? reservation = await coordinator.TrySwitchToCanonicalReplayAsync(identity,
                    original.Projection, reservations[original.Projection], cancellationToken).ConfigureAwait(false);
                if (reservation?.GuardBinding is not { } binding) return new(true, false, reason = "delivery_in_progress");
                reservations[original.Projection] = reservation;
                plans[planIndex] = original with { Binding = binding, Admitted = replay.Tail, ReplayEvents = replay.Events };
            }
            // Collect every explicit no-effect fallback before preparing the one
            // original coordinated manifest. Already-proven tail siblings stay intact.
            if (plans.Any(static plan => plan.ReplayEvents is not null) && (completedReplayOwners.Length == 0
                || plans.Any(plan => plan.ReplayEvents is not null && (plan.ObservedOwner?.Selection.AppliedSequence != head.HeadSequence
                    || plan.ObservedOwner.Selection.BatchId != work.DispatchId || plan.ObservedOwner.Selection.ReceiptProjectionName != "rebuild"))))
            {
                string? replayFailure = await ApplyCanonicalReplayAsync(identity, catalog, plans, snapshots,
                    work.DispatchId, cancellationToken).ConfigureAwait(false);
                if (replayFailure is not null) return new(true, false, reason = replayFailure);
            }

            // Publication has consumed the complete canonical prefix. Retain
            // only its mode marker and immutable digest tuples while the
            // verification pass decrypts its next bounded page.
            sharedReplayEvents = null;
            for (int index = 0; index < plans.Count; index++)
                if (plans[index].ReplayEvents is not null) plans[index] = plans[index] with { ReplayEvents = [] };

            bool allCurrent = true;
            var replayOwners = new List<ProjectionOwnerProof>(completedReplayOwners);
            var verifiedOwners = new Dictionary<string, ProjectionOwnerProof>(StringComparer.Ordinal);
            foreach (ProjectionSourceGuardTailPlan plan in plans)
            {
                if (plan.ReplayEvents is not null)
                    _ = await history.AdmitReplayAsync(identity, plan.Projection, plan.State, head, pageSize, false, cancellationToken,
                        expectedAdmission: plan.Admitted).ConfigureAwait(false);
                else
                    _ = await history.AdmitAsync(identity, plan.Projection, plan.Binding.BaseSource,
                        head, plan.State.CompletedPrefixFingerprint!, pageSize, cancellationToken, plan.Admitted).ConfigureAwait(false);
                ProjectionOwnerProofResult output = await owners.ReadAsync(identity, catalog, plan.Projection,
                    snapshots[plan.Projection], cancellationToken).ConfigureAwait(false);
                if (output.Proof is not { } selected || !ValidProgress(identity, plan, selected, head.HeadSequence)
                    || selected.Selection.MessageId != head.HeadMessageId || selected.Selection.BatchId != head.HeadMessageId
                    || plan.ReplayEvents is not null && selected.Selection.ReceiptProjectionName != "rebuild")
                    return new(true, false, reason = output.ReasonCode ?? "delivery_reconciliation_required");
                verifiedOwners.Add(plan.Projection, selected);
            }
            // Every sibling's frozen-history and output checks use the caller's
            // verification lifetime. Only the shared completion/cleanup phase
            // consumes the independent deadline.
            telemetry?.StartFinalization(clock);
            finalizationBudget = new(TimeSpan.FromSeconds(30), clock);
            foreach (ProjectionSourceGuardTailPlan plan in plans)
            {
                ProjectionOwnerProof selected = verifiedOwners[plan.Projection];
                ProjectionSourceGuardDeliveryResult final = await finalizer.FinalizeAsync(identity, catalog, plan.Projection,
                    plan.Admitted.History, reservations[plan.Projection], operation, selected, cancellationToken,
                    finalizationBudget.Token).ConfigureAwait(false);
                finalObservations[plan.Projection] = final;
                if (final.CompletionProven)
                {
                    completedRoutes.Add(plan.Projection);
                    held.Remove(plan.Projection);
                    if (plan.ReplayEvents is not null) replayOwners.Add(selected);
                }
                allCurrent &= final.AlreadyCurrent;
                if (!final.AlreadyCurrent) reason = final.ReasonCode ?? "delivery_reconciliation_required";
            }
            if (replayOwners.Count > 0)
            {
                replayCleanupComplete = false;
                if (completedRoutes.Count == plans.Count)
                    replayCleanupComplete = await transport.ReclaimReplayAsync(catalog,
                        ProjectionSourceGuardReplayCleanup.CreateRequest(identity, catalog, replayOwners),
                        finalizationBudget!.Token).ConfigureAwait(false);
            }
            return new(true, allCurrent && head == observedHead, allCurrent && head == observedHead ? null : reason)
            { FinalizedCleanupProven = allCurrent && head == observedHead && replayCleanupComplete };
        }
        catch (ProjectionDeliveryHistoryValidationException exception)
        {
            reason = exception.Message is "delivery_identity_conflict" or "delivery_gap" or "history_unavailable" or "source_conflict"
                or "delivery_page_limit_exceeded" or "delivery_byte_limit_exceeded" or "delivery_owner_limit_exceeded"
                or "delivery_proof_limit_exceeded" ? exception.Message : "delivery_state_unavailable";
            throw;
        }
        catch (OperationCanceledException) { reason = "delivery_reconciliation_required"; throw; }
        catch { reason = "delivery_state_unavailable"; throw; }
        finally
        {
            using var cleanup = finalizationBudget ?? new CancellationTokenSource(TimeSpan.FromSeconds(30), clock);
            foreach (ProjectionSourceGuardTailPlan plan in plans)
            {
                string mode =
                    plan.ReplayEvents is null ? "tail" : catalog.MappedOnlyStreamedProjectionTypes.Contains(plan.Projection, StringComparer.Ordinal)
                        ? "mapped_replay" : "full_replay";
                string status = completedRoutes.Contains(plan.Projection) ? replayCleanupComplete ? "completed" : "retryable" : "indeterminate";
                finalObservations.TryGetValue(plan.Projection, out ProjectionSourceGuardDeliveryResult? final);
                string outcome = final?.ReasonCode ?? (completedRoutes.Contains(plan.Projection) && replayCleanupComplete ? "delivery_completed" : reason);
                if (final?.ReasonCode is "delivery_state_unavailable" or "delivery_reconciliation_required" or "source_conflict") status = "indeterminate";
                long latestHead = final?.FinalSourceHeadSequence ?? head.HeadSequence;
                long checkpoint = completedRoutes.Contains(plan.Projection) ? head.HeadSequence : plan.State.LastDeliveredSequence;
                if (telemetry is null) ProjectionDeliveryDiagnostics.RecordGuarded(plan.Projection, mode, status, outcome, latestHead, checkpoint);
                else telemetry.Terminal(plan.Projection, mode, status, outcome, latestHead, checkpoint);
            }
            foreach (string route in held)
                await lifecycle.CompleteDeliveryWriteAsync(identity, route, operation, cleanup.Token).ConfigureAwait(false);
            bool coordinatedReplayPending = (completedReplayOwners.Length > 0 || plans.Any(static plan => plan.ReplayEvents is not null))
                && (completedRoutes.Count != plans.Count || !replayCleanupComplete);
            string[] pending = [.. work.PendingRoutes.Where(route => coordinatedReplayPending || !completedRoutes.Contains(route)
                && !completedReplayOwners.Any(owner => owner.Selection.ProjectionName == route))];
            if (pending.Length == 0 && work.TerminalRoutes.Count == 0)
            {
                if (!await scheduler.TryDeleteAsync(work, cleanup.Token).ConfigureAwait(false))
                {
                    int attempt = work.Attempt < options.MaxRetryAttempts ? work.Attempt + 1 : options.MaxRetryAttempts;
                    await scheduler.TryUpdateAsync(work with { Attempt = attempt,
                        NextDueUtc = clock.GetUtcNow() + GetRetryDelay(attempt, options),
                        ReservationFencingTokens = work.ReservationFencingTokens.Concat(reservations.Select(static pair =>
                            new KeyValuePair<string, long>(pair.Key, pair.Value.FencingToken)))
                            .GroupBy(static pair => pair.Key, StringComparer.Ordinal).ToDictionary(static group => group.Key, static group => group.Last().Value, StringComparer.Ordinal),
                    }, cleanup.Token).ConfigureAwait(false);
                }
            }
            else
            {
                int attempt = work.Attempt < options.MaxRetryAttempts ? work.Attempt + 1 : options.MaxRetryAttempts;
                await scheduler.TryUpdateAsync(work with
                {
                    PendingRoutes = pending, LastReasonCode = reason,
                    Attempt = attempt, NextDueUtc = clock.GetUtcNow() + GetRetryDelay(attempt, options),
                    ReservationFencingTokens = work.ReservationFencingTokens.Concat(reservations.Select(static pair =>
                        new KeyValuePair<string, long>(pair.Key, pair.Value.FencingToken)))
                        .Where(pair => pending.Contains(pair.Key, StringComparer.Ordinal))
                        .GroupBy(static pair => pair.Key, StringComparer.Ordinal).ToDictionary(static group => group.Key, static group => group.Last().Value, StringComparer.Ordinal),
                }, cleanup.Token).ConfigureAwait(false);
            }
        }
    }

    /// <summary>Preserves the existing bounded exponential backoff without imposing a lifetime activation cap.</summary>
    internal static TimeSpan GetRetryDelay(int attempt, ProjectionDispatchOptions options)
        => TimeSpan.FromTicks((long)Math.Min(options.RetryMaxDelay.Ticks,
            options.RetryBaseDelay.Ticks * Math.Pow(2, Math.Max(0, attempt - 1))));

    /// <summary>Requires the negotiated recovery descriptor and the same catalog's declared handler semantics.</summary>
    internal static bool SupportsRoute(NamedProjectionRouteCatalogEntry catalog, string route, ProjectionSourceGuardCapability capability)
        => capability.IsSupported && (capability.HandlerSemantics == "single-event-v1"
            ? catalog.SingleEventProjectionTypes.Contains(route, StringComparer.Ordinal)
            : capability.HandlerSemantics == "mapped-incremental-replay-v1"
                ? catalog.IncrementalReplayProjectionTypes.Contains(route, StringComparer.Ordinal)
                : capability.HandlerSemantics == "full-replay-v1" && catalog.FullReplayProjectionTypes.Contains(route, StringComparer.Ordinal));

    private static bool SupportsCanonicalReplay(NamedProjectionRouteCatalogEntry catalog, string route)
        => catalog.FullReplayProjectionTypes.Contains(route, StringComparer.Ordinal)
            || catalog.SourceGuardCapabilities.GetValueOrDefault(route)?.CanonicalReplayProtocol == "staged-full-replay/v1";

    private static bool ValidProgress(AggregateIdentity identity, ProjectionSourceGuardTailPlan plan, ProjectionOwnerProof proof, long sequence)
        => ProjectionDeliveryIdempotencyCoordinator.ValidOwnerProof(proof, identity, plan.Projection)
            && proof.Selection.OwnerIdentity == (plan.ObservedOwner?.Selection.OwnerIdentity ?? identity.AggregateId)
            && proof.Selection.OwnerGeneration == plan.Binding.SelectedOwnerGeneration && proof.Selection.AppliedSequence == sequence;

    private async Task<ProjectionOwnerProofResult> ReconcileOriginalProgressAsync(AggregateIdentity identity,
        NamedProjectionRouteCatalogEntry catalog, ProjectionSourceGuardTailPlan plan, ProjectionLifecycleSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        long completed = plan.Binding.BaseSource.HeadSequence;
        ProjectionOwnerProof? last = plan.Binding.BaseOwnerProof;
        foreach (ProjectionSourceAdmittedEvent item in plan.Admitted.Events)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await lifecycle.ReadSnapshotAsync(identity, plan.Projection, cancellationToken).ConfigureAwait(false) != snapshot)
                return new(null, "delivery_in_progress", 0, 0);
            var scope = new ProjectionOwnerProofRequest(identity.TenantId, identity.Domain, identity.AggregateId,
                plan.Projection, catalog.CatalogFingerprint);
            ProjectionOwnerProofResult receipt = await transport.ReconcileAppliedReceiptAsync(catalog,
                new(scope, item.Sequence, item.MessageId, plan.ObservedOwner?.Selection.OwnerIdentity ?? identity.AggregateId,
                    plan.Binding.SelectedOwnerGeneration), cancellationToken).ConfigureAwait(false);
            if (receipt.Proof is null && receipt.ReasonCode == "delivery_rebuild_required") break;
            if (receipt.Proof is not { } proof || !ValidProgress(identity, plan, proof, item.Sequence)
                || proof.Selection.MessageId != item.MessageId || proof.Selection.BatchId != item.MessageId)
                return new(null, receipt.ReasonCode ?? "delivery_reconciliation_required", receipt.MetadataKeyReads, receipt.ReadBodyBytes);
            completed = item.Sequence;
            last = proof;
        }
        ProjectionOwnerProofResult selected = await owners.ReadAsync(identity, catalog, plan.Projection, snapshot, cancellationToken).ConfigureAwait(false);
        if (completed == 0 && last is null && selected.Proof is null && selected.ReasonCode == "delivery_rebuild_required") return selected;
        return selected.Proof is { } owner && owner == last && ValidProgress(identity, plan, owner, completed)
            ? selected : new(null, selected.ReasonCode ?? "delivery_reconciliation_required", selected.MetadataKeyReads, selected.ReadBodyBytes);
    }

    private static bool Matches(ProjectionSourceGuardTailPlan plan, ProjectionEventDto item, string sourceHash)
    {
        long offset = item.SequenceNumber - plan.Binding.BaseSource.HeadSequence - 1;
        if (offset < 0 || offset >= plan.Admitted.Events.Count) return false;
        ProjectionSourceAdmittedEvent expected = plan.Admitted.Events[(int)offset];
        return expected.Sequence == item.SequenceNumber && expected.MessageId == item.MessageId && expected.SourceEventDigest == sourceHash
            && expected.RouteEventFingerprint == ProjectionDeliveryFingerprint.ComputeEvent(item,
                plan.Admitted.History.InitialFingerprint.StartsWith("v2:", StringComparison.Ordinal));
    }
}
