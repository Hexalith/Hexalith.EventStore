using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Events;

namespace Hexalith.EventStore.Server.Projections;

internal sealed partial class ProjectionSourceGuardQualification
{
    private async Task<ProjectionSourceGuardQualificationResult> QualifyEmptyAsync(AggregateIdentity identity,
        string projection, long expectedHead, string expectedIncarnation, ProjectionSourceGuardMaintenance maintenance,
        bool activate, string? previousIncarnation, NamedProjectionRouteCatalogEntry catalog,
        ProjectionSourceGuardCapability capability, IAggregateActor source, SourceProofInspection before,
        ProjectionDeliveryStateReadResult read, CancellationToken cancellationToken)
    {
        if (expectedHead != 0 || before.Status != SourceProofInspectionStatus.Available || before.Erased
            || before.Proof is not { HeadSequence: 0, RetainedFloor: 1, Erased: false } seed
            || seed.SourceIncarnation != expectedIncarnation || seed != SourceProofCodec.CreateSeed(identity, seed.SourceIncarnation)
            || !read.WriterProtocolV2Active)
            return new(false, false, "source_proof_missing", 0);
        ProjectionDeliveryState? original = read.State;
        string initial = ProjectionDeliveryFingerprint.ComputeInitial(identity, projection,
            original?.CompletedPrefixFingerprint?.StartsWith("v2:", StringComparison.Ordinal) == true);
        if (original is not null && (original.LastDeliveredSequence != 0 || original.GuardEvidence is not null
            || original.ActiveReservation is not null || original.IdleBindingPending is not null
            || read.Classification != ProjectionDeliveryStateClassification.Current
            || !ProjectionDeliveryIdempotencyCoordinator.IsValidCurrentState(original, identity, projection, initial)
            || !ProjectionDeliveryIdempotencyCoordinator.ValidCompleteIdentityDigests(original)))
            return new(false, false, "checkpoint_drift", original.LastDeliveredSequence);
        if (read.SourceGuard is { } existing && (existing.SourceIncarnation != seed.SourceIncarnation
            ? existing.SourceIncarnation != previousIncarnation
            : existing.CatalogFingerprint != catalog.CatalogFingerprint || existing.HandlerSemantics != capability.HandlerSemantics
                || existing.CutoverCommit != maintenance.CutoverCommit))
            return new(false, false, "guard_activation_required", 0);
        ProjectionLifecycleSnapshot idle = await lifecycle.ReadSnapshotAsync(identity, projection, cancellationToken).ConfigureAwait(false);
        ProjectionOwnerProofResult model = await owners.ReadAsync(identity, catalog, projection, idle, cancellationToken).ConfigureAwait(false);
        if (idle.Phase != ProjectionLifecyclePhase.Idle || model.Proof is not null || model.ReasonCode != "delivery_rebuild_required")
            return new(false, false, "checkpoint_drift", 0);
        if (await source.InspectSourceProofAsync().WaitAsync(cancellationToken).ConfigureAwait(false) != before
            || await lifecycle.ReadSnapshotAsync(identity, projection, cancellationToken).ConfigureAwait(false) != idle)
            return new(false, false, "head_changed", 0);
        ProjectionDeliveryState qualified = (original ?? ProjectionDeliveryState.CreateEmpty(identity, projection, initial, clock.GetUtcNow())) with
        { SchemaVersion = 2, GuardEvidenceVersion = 1, UpdatedAt = original?.SchemaVersion == 2 ? original.UpdatedAt : clock.GetUtcNow() };
        ProjectionSourceGuard marker = read.SourceGuard?.SourceIncarnation == seed.SourceIncarnation ? read.SourceGuard
            : new(1, 1, 2, 2, 1, identity.TenantId, identity.Domain, identity.AggregateId, projection, seed.SourceIncarnation,
                catalog.CatalogFingerprint, capability.HandlerSemantics, maintenance.SpecSha256, maintenance.CutoverCommit, clock.GetUtcNow());
        var work = new ProjectionDeliveryReconciliationWork(identity.TenantId, identity.Domain, identity.AggregateId, projection,
            "empty_no_work", 0, 2, maintenance.OperatorId, clock.GetUtcNow())
        { SourceGuardMaintenance = maintenance, QualifiedSourceGuard = marker };
        for (int attempt = 0; attempt < options.Value.MaxStateTransitionAttempts; attempt++)
        {
            bool saved = false;
            try
            {
                saved = activate
                    ? await states.TryActivateSourceGuardAsync(identity, projection, qualified, marker, work,
                        read.Etag, read.SourceGuardEtag, cancellationToken).ConfigureAwait(false)
                    : await states.TrySaveWithReconciliationAsync(identity, projection, qualified, work, read.Etag, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { throw; }
            catch (ProjectionDeliveryHistoryValidationException) { throw; }
            catch { /* Resolve an ambiguous same-component transaction by persisted readback. */ }
            ProjectionDeliveryStateReadResult durable = await states.ReadAsync(identity, projection, cancellationToken).ConfigureAwait(false);
            if (EquivalentQualification(durable.State, qualified) && (!activate || durable.SourceGuard == marker))
            {
                ProjectionDeliveryReconciliationWork? audit = await states.ReadReconciliationAsync(identity, projection, cancellationToken).ConfigureAwait(false);
                if (audit?.QualifiedSourceGuard != marker || audit.SourceGuardMaintenance is not { } attestation
                    || audit.OperatorId != maintenance.OperatorId || !SameMaintenance(attestation, maintenance))
                    return new(false, false, "delivery_reconciliation_required", 0);
                if (await source.InspectSourceProofAsync().WaitAsync(cancellationToken).ConfigureAwait(false) != before
                    || await lifecycle.ReadSnapshotAsync(identity, projection, cancellationToken).ConfigureAwait(false) != idle)
                    return new(false, false, "head_changed", 0);
                return new(true, activate, "empty_no_work", 0);
            }
            if (saved || (original is null ? durable.State is not null : !EquivalentQualification(durable.State, original))
                || !durable.WriterProtocolV2Active || durable.SourceGuard != read.SourceGuard)
                return new(false, false, "delivery_reconciliation_required", 0);
            read = durable;
        }
        return new(false, false, "delivery_state_unavailable", 0);
    }
}
