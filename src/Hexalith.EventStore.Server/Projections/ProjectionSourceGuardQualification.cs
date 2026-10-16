using System.Security.Cryptography;
using System.Text;

using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Events;

using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>Qualifies the existing checkpoint through canonical source and selected-model proof, without advancement.</summary>
internal sealed partial class ProjectionSourceGuardQualification(IProjectionDeliveryStateStore states,
    INamedProjectionRouteCatalog catalogs, IActorProxyFactory actors, IOptions<EventStoreActorOptions> actorOptions,
    ProjectionSourceGuardHistoryReader history, IProjectionLifecycleGateway lifecycle,
    IProjectionSelectedOwnerProofGateway owners, IOptions<ProjectionDeliveryIdempotencyOptions> options, TimeProvider clock)
{
    /// <summary>Hydrates schema2 and, only when explicitly requested, CAS-activates this exact qualified scope.</summary>
    internal async Task<ProjectionSourceGuardQualificationResult> QualifyAsync(AggregateIdentity identity, string projection,
        long expectedHead, string expectedIncarnation, ProjectionSourceGuardMaintenance maintenance, bool activate,
        string? expectedPreviousIncarnation = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(maintenance);
        ProjectionKeySegments.Validate(projection, nameof(projection));
        if (!maintenance.IsValid) return new(false, false, "guard_activation_required", 0);
        NamedProjectionRouteCatalogEntry[] matches = [.. catalogs.Current.Entries.Where(entry => entry.Domain == identity.Domain
            && entry.OrdinaryProjectionTypes.Contains(projection, StringComparer.Ordinal))];
        if (matches.Length != 1 || !matches[0].SourceGuardCapabilities.TryGetValue(projection, out ProjectionSourceGuardCapability? capability)
            || !ProjectionSourceGuardTailDelivery.SupportsRoute(matches[0], projection, capability))
            return new(false, false, "unsupported_capability", 0);
        // Only implemented runtime protocols can be admitted. Qualification never converts a handler.
        NamedProjectionRouteCatalogEntry catalog = matches[0];
        options.Value.Validate();
        IAggregateActor source = actors.CreateActorProxy<IAggregateActor>(new ActorId(identity.ActorId), actorOptions.Value.AggregateActorTypeName);
        long preserved = 0;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            SourceProofInspection before = await source.InspectSourceProofAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
            ProjectionDeliveryStateReadResult read = await states.ReadAsync(identity, projection, cancellationToken).ConfigureAwait(false);
            ProjectionDeliveryState? original = read.State;
            preserved = original?.LastDeliveredSequence ?? 0;
            if (before.CurrentSequence == 0)
            {
                if (capability.InitialOwnerProtocol == "aggregate-first-event/v1")
                    return await QualifyEmptyAsync(identity, projection, expectedHead, expectedIncarnation, maintenance,
                        activate, expectedPreviousIncarnation, catalog, capability, source, before, read, cancellationToken).ConfigureAwait(false);
                ProjectionLifecycleSnapshot emptyLifecycle = await lifecycle.ReadSnapshotAsync(identity, projection, cancellationToken).ConfigureAwait(false);
                ProjectionOwnerProofResult emptyModel = await owners.ReadAsync(identity, catalog, projection, emptyLifecycle, cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                if (await source.InspectSourceProofAsync().WaitAsync(cancellationToken).ConfigureAwait(false) != before)
                    return new(false, false, "head_changed", preserved);
                return new(false, false, original is null && !before.Erased && emptyLifecycle.Phase == ProjectionLifecyclePhase.Idle
                    && emptyModel.Proof is null && emptyModel.ReasonCode == "delivery_rebuild_required"
                        ? "empty_no_work" : "delivery_rebuild_required", preserved);
            }
            if (before.Status != SourceProofInspectionStatus.Available || before.Proof is not { } head)
                return new(false, false, "source_proof_missing", preserved);
            if (head.Erased || head.SourceIncarnation != expectedIncarnation || head.HeadSequence != expectedHead)
                return new(false, false, "source_conflict", preserved);
            if (head.HeadSequence > 10_000) return new(false, false, "delivery_owner_limit_exceeded", preserved);
            if (head.RetainedFloor != 1) return new(false, false, "history_unavailable", preserved);
            if (!read.WriterProtocolV2Active || original is null || read.Classification != ProjectionDeliveryStateClassification.Current
                || preserved <= 0 || preserved > head.HeadSequence || original.ActiveReservation is not null || original.IdleBindingPending is not null)
                return new(false, false, "delivery_reconciliation_required", preserved);
            bool v2 = original.CompletedPrefixFingerprint?.StartsWith("v2:", StringComparison.Ordinal) == true;
            string initial = ProjectionDeliveryFingerprint.ComputeInitial(identity, projection, v2);
            if (!ProjectionDeliveryIdempotencyCoordinator.IsValidCurrentState(original, identity, projection, initial)
                || !ProjectionDeliveryIdempotencyCoordinator.ValidCompleteIdentityDigests(original))
                return new(false, false, "delivery_reconciliation_required", preserved);
            if (read.SourceGuard is { } existing && (existing.SourceIncarnation != head.SourceIncarnation
                ? existing.SourceIncarnation != expectedPreviousIncarnation
                : existing.CatalogFingerprint != catalog.CatalogFingerprint || existing.HandlerSemantics != capability.HandlerSemantics
                    || existing.CutoverCommit != maintenance.CutoverCommit))
                return new(false, false, "guard_activation_required", preserved);
            ProjectionLifecycleSnapshot idle = await lifecycle.ReadSnapshotAsync(identity, projection, cancellationToken).ConfigureAwait(false);
            if (idle.Phase != ProjectionLifecyclePhase.Idle || idle.Revision <= 0)
                return new(false, false, "delivery_in_progress", preserved);
            ProjectionOwnerProofResult selected = await owners.ReadAsync(identity, catalog, projection, idle, cancellationToken).ConfigureAwait(false);
            if (selected.Proof is not { } owner || !ProjectionDeliveryIdempotencyCoordinator.ValidOwnerProof(owner, identity, projection)
                || owner.Selection.AppliedSequence != preserved || owner.Selection.MessageId != original.LastCompletedMessageId)
                return new(false, false, selected.ReasonCode ?? "delivery_rebuild_required", preserved);

            SourceProof chain = SourceProofCodec.CreateSeed(identity, head.SourceIncarnation);
            SourceProof? atCheckpoint = null;
            string prefix = initial;
            string? completedPrefix = null;
            var messageIds = new HashSet<string>(StringComparer.Ordinal);
            await foreach (ProjectionSourceGuardPage page in history.ReadPagesAsync(identity, chain, head, 256, cancellationToken).ConfigureAwait(false))
            {
                for (int index = 0; index < page.Stored.Length; index++)
                {
                    chain = SourceProofCodec.ExtendVerified(chain, page.Stored[index], page.SourceEventDigests[index]);
                    ProjectionEventDto item = page.Readable[index];
                    if (string.IsNullOrWhiteSpace(item.MessageId) || !messageIds.Add(item.MessageId))
                        return new(false, false, "source_conflict", preserved);
                    string eventHash = ProjectionDeliveryFingerprint.ComputeEvent(item, v2);
                    prefix = ProjectionDeliveryFingerprint.Extend(prefix, eventHash);
                    if (item.SequenceNumber <= preserved)
                    {
                        string idDigest = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(item.MessageId!)));
                        if (original.CompletedMessageIdDigests![checked((int)item.SequenceNumber - 1)] != idDigest)
                            return new(false, false, "source_conflict", preserved);
                        if (item.SequenceNumber >= original.FirstRetainedSequence)
                        {
                            ProjectionDeliveryReceipt receipt = original.CompletedReceipts![checked((int)(item.SequenceNumber - original.FirstRetainedSequence))];
                            if (receipt.MessageId != item.MessageId || receipt.EventFingerprint != eventHash || receipt.PrefixFingerprint != prefix)
                                return new(false, false, "source_conflict", preserved);
                        }
                    }
                    if (item.SequenceNumber == preserved) { atCheckpoint = chain; completedPrefix = prefix; }
                }
            }
            if (atCheckpoint is null || completedPrefix != original.CompletedPrefixFingerprint
                || atCheckpoint.HeadMessageId != original.LastCompletedMessageId)
                return new(false, false, "source_conflict", preserved);
            cancellationToken.ThrowIfCancellationRequested();
            if (await source.InspectSourceProofAsync().WaitAsync(cancellationToken).ConfigureAwait(false) != before
                || await lifecycle.ReadSnapshotAsync(identity, projection, cancellationToken).ConfigureAwait(false) != idle)
                return new(false, false, "head_changed", preserved);
            ProjectionOwnerProofResult rechecked = await owners.ReadAsync(identity, catalog, projection, idle, cancellationToken).ConfigureAwait(false);
            if (rechecked.Proof != owner) return new(false, false, rechecked.ReasonCode ?? "delivery_reconciliation_required", preserved);
            var evidence = new ProjectionDeliveryGuardEvidence(atCheckpoint, catalog.CatalogFingerprint,
                capability.HandlerSemantics, owner, idle.Revision);
            if (original.SchemaVersion == 2 && (original.GuardEvidenceVersion != 1
                || original.GuardEvidence is not { } previous || previous.Source != evidence.Source
                || previous.CatalogFingerprint != evidence.CatalogFingerprint || previous.HandlerSemantics != evidence.HandlerSemantics
                || previous != evidence && (owner.Selection.ReceiptProjectionName != "rebuild"
                    || owner.Selection.OwnerIdentity != previous.OwnerProof.Selection.OwnerIdentity
                    || idle.Revision <= previous.IdleLifecycleRevision)))
                return new(false, false, "source_conflict", preserved);
            ProjectionDeliveryState qualified = original with
            {
                SchemaVersion = 2, GuardEvidenceVersion = 1, GuardEvidence = evidence,
                UpdatedAt = original.SchemaVersion == 2 && original.GuardEvidence == evidence ? original.UpdatedAt : clock.GetUtcNow(),
            };
            DateTimeOffset now = clock.GetUtcNow();
            ProjectionSourceGuard marker = read.SourceGuard?.SourceIncarnation == head.SourceIncarnation ? read.SourceGuard
                : new(1, 1, 2, 2, 1, identity.TenantId, identity.Domain, identity.AggregateId, projection,
                    head.SourceIncarnation, catalog.CatalogFingerprint, capability.HandlerSemantics,
                    maintenance.SpecSha256, maintenance.CutoverCommit, now);
            var work = new ProjectionDeliveryReconciliationWork(identity.TenantId, identity.Domain, identity.AggregateId,
                projection, "delivery_reconciled", preserved, 2, maintenance.OperatorId, now)
            { SourceGuardMaintenance = maintenance, QualifiedSourceGuard = marker };

            // Canonical history is read once. CAS conflicts reclassify the same immutable proof;
            // never restart history within this activation or overwrite a newer reservation.
            for (int attempt = 0; attempt < options.Value.MaxStateTransitionAttempts; attempt++)
            {
                bool saved = false;
                try
                {
                    saved = activate
                        ? await states.TryActivateSourceGuardAsync(identity, projection, qualified, marker, work,
                            read.Etag, read.SourceGuardEtag, cancellationToken).ConfigureAwait(false)
                        : await states.TrySaveWithReconciliationAsync(identity, projection, qualified, work,
                            read.Etag, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { throw; }
                catch (ProjectionDeliveryHistoryValidationException) { throw; }
                catch { /* An ambiguous transaction is decided only by persisted readback. */ }
                ProjectionDeliveryStateReadResult durable = await states.ReadAsync(identity, projection, cancellationToken).ConfigureAwait(false);
                if (EquivalentQualification(durable.State, qualified) && (!activate || durable.SourceGuard == marker))
                {
                    ProjectionDeliveryReconciliationWork? audit = await states.ReadReconciliationAsync(identity, projection, cancellationToken).ConfigureAwait(false);
                    if (audit?.OperatorId != maintenance.OperatorId || audit.QualifiedSourceGuard != marker
                        || audit.SourceGuardMaintenance is not { } recorded || !SameMaintenance(recorded, maintenance))
                        return new(false, false, "delivery_reconciliation_required", preserved);
                    cancellationToken.ThrowIfCancellationRequested();
                    if (await source.InspectSourceProofAsync().WaitAsync(cancellationToken).ConfigureAwait(false) != before
                        || await lifecycle.ReadSnapshotAsync(identity, projection, cancellationToken).ConfigureAwait(false) != idle)
                        return new(false, false, "head_changed", preserved);
                    return new(true, activate, "delivery_reconciled", preserved);
                }
                if (saved || !EquivalentQualification(durable.State, original) || !durable.WriterProtocolV2Active
                    || durable.SourceGuard != read.SourceGuard)
                    return new(false, false, "delivery_reconciliation_required", preserved);
                read = durable;
            }
            return new(false, false, "delivery_state_unavailable", preserved);
        }
        catch (OperationCanceledException) { throw; }
        catch (ProjectionDeliveryHistoryValidationException exception)
        {
            return new(false, false, exception.Message is "delivery_page_limit_exceeded" or "delivery_byte_limit_exceeded"
                or "delivery_owner_limit_exceeded" or "delivery_proof_limit_exceeded" or "history_unavailable"
                ? exception.Message : "source_conflict", preserved);
        }
        catch { return new(false, false, "delivery_state_unavailable", preserved); }
    }

    private static bool EquivalentQualification(ProjectionDeliveryState? left, ProjectionDeliveryState right)
        => left is not null && left.SchemaVersion == right.SchemaVersion && left.WriterProtocolVersion == right.WriterProtocolVersion
            && left.TenantId == right.TenantId && left.Domain == right.Domain && left.AggregateId == right.AggregateId
            && left.ProjectionName == right.ProjectionName && left.LastDeliveredSequence == right.LastDeliveredSequence
            && left.LastCompletedMessageId == right.LastCompletedMessageId && left.CompletedPrefixFingerprint == right.CompletedPrefixFingerprint
            && left.FirstRetainedSequence == right.FirstRetainedSequence && left.ActiveReservation == right.ActiveReservation
            && left.IdleBindingPending == right.IdleBindingPending && left.GuardEvidenceVersion == right.GuardEvidenceVersion
            && left.GuardEvidence == right.GuardEvidence && left.MigrationProvenance == right.MigrationProvenance
            && left.UpdatedAt == right.UpdatedAt && (left.CompletedReceipts ?? []).SequenceEqual(right.CompletedReceipts ?? [])
            && (left.CompletedMessageIdDigests ?? []).SequenceEqual(right.CompletedMessageIdDigests ?? [], StringComparer.Ordinal);

    private static bool SameMaintenance(ProjectionSourceGuardMaintenance left, ProjectionSourceGuardMaintenance right)
        => left.IsValid && left.OperatorId == right.OperatorId && left.CutoverCommit == right.CutoverCommit
            && left.BackupReference == right.BackupReference && left.SpecSha256 == right.SpecSha256
            && left.TrafficStopped == right.TrafficStopped && left.BackupRestoreVerified == right.BackupRestoreVerified
            && left.DowngradeProhibited == right.DowngradeProhibited && left.WriterInventory.SequenceEqual(right.WriterInventory);
}
