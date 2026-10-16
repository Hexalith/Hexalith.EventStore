using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Events;

using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>Owns the pending, release, observed Idle, conditional completion and readback boundary.</summary>
internal sealed class ProjectionSourceGuardFinalizer(IProjectionDeliveryIdempotencyCoordinator coordinator,
    IProjectionDeliveryStateStore states, IProjectionLifecycleGateway lifecycle,
    IProjectionSelectedOwnerProofGateway owners, ProjectionSourceGuardHistoryReader history,
    IActorProxyFactory actors, IOptions<EventStoreActorOptions> actorOptions, TimeProvider? timeProvider = null)
{
    private readonly TimeProvider _clock = timeProvider ?? TimeProvider.System;
    /// <summary>Prepares proven output without advancing C, then finishes using a bounded independent token.</summary>
    internal async Task<ProjectionSourceGuardDeliveryResult> FinalizeAsync(AggregateIdentity identity,
        NamedProjectionRouteCatalogEntry catalog, string projection, ProjectionDeliveryFingerprintHistory admitted,
        ProjectionDeliveryReservation reservation, string operationToken, ProjectionOwnerProof selected,
        CancellationToken cancellationToken, CancellationToken finalizationToken = default)
    {
        using CancellationTokenSource? budget = finalizationToken.CanBeCanceled ? null : new(TimeSpan.FromSeconds(30), _clock);
        CancellationToken independent = finalizationToken.CanBeCanceled ? finalizationToken : budget!.Token;
        independent.ThrowIfCancellationRequested();
        if (!await coordinator.PrepareIdleBindingAsync(identity, projection, reservation, operationToken, selected,
            independent).ConfigureAwait(false)) return new(true, false, "delivery_reconciliation_required");
        return await ReleaseAndBindAsync(identity, catalog, projection, admitted, reservation, operationToken,
            selected, independent).ConfigureAwait(false);
    }

    /// <summary>Recovers the same frozen pending work without handler invocation or logical model writes.</summary>
    internal async Task<ProjectionSourceGuardDeliveryResult> RecoverAsync(AggregateIdentity identity,
        NamedProjectionRouteCatalogEntry catalog, string projection, ProjectionDeliveryState observed,
        int pageSize, CancellationToken cancellationToken, long? resumeFencingToken = null, CancellationToken finalizationToken = default)
    {
        if (observed.ActiveReservation is not { GuardBinding: { } binding } original
            || observed.IdleBindingPending is not { } pending)
            return new(true, false, "delivery_reconciliation_required");
        string operation = Guid.NewGuid().ToString("N", System.Globalization.CultureInfo.InvariantCulture);
        if (!await lifecycle.BeginDeliveryWriteAsync(identity, projection, operation, cancellationToken).ConfigureAwait(false))
            return new(true, false, "delivery_in_progress");
        bool releaseOwned = true;
        try
        {
            ProjectionDeliveryReservation? reclaimed = await coordinator.TryReclaimIdleBindingAsync(identity, projection,
                original, operation, cancellationToken, resumeFencingToken).ConfigureAwait(false);
            if (reclaimed is null) return new(true, false, "delivery_in_progress");
            ProjectionLifecycleSnapshot delivering = await lifecycle.ReadSnapshotAsync(identity, projection, cancellationToken).ConfigureAwait(false);
            ProjectionOwnerProofResult selected = await owners.ReadAsync(identity, catalog, projection, delivering, cancellationToken).ConfigureAwait(false);
            if (delivering.Phase != ProjectionLifecyclePhase.Delivering || selected.Proof != pending.OwnerProof)
                return new(true, false, selected.ReasonCode ?? "delivery_reconciliation_required");
            ProjectionSourceAdmittedHistory admitted = await history.AdmitAsync(identity, projection, binding.BaseSource,
                binding.AdmittedSource, observed.CompletedPrefixFingerprint!, pageSize, cancellationToken).ConfigureAwait(false);
            if (admitted.History.PrefixFingerprint != original.ManifestFingerprint)
                return new(true, false, "source_conflict");
            // From here no domain effects are possible. Even a canceled caller may finish readback
            // and fencing for at most thirty seconds; the persisted pending row survives timeout.
            using CancellationTokenSource? finalization = finalizationToken.CanBeCanceled ? null : new(TimeSpan.FromSeconds(30), _clock);
            releaseOwned = false;
            return await ReleaseAndBindAsync(identity, catalog, projection, admitted.History, reclaimed, operation,
                pending.OwnerProof, finalizationToken.CanBeCanceled ? finalizationToken : finalization!.Token).ConfigureAwait(false);
        }
        finally
        {
            if (releaseOwned)
            {
                using CancellationTokenSource? cleanup = finalizationToken.CanBeCanceled ? null : new(TimeSpan.FromSeconds(30), _clock);
                await lifecycle.CompleteDeliveryWriteAsync(identity, projection, operation,
                    finalizationToken.CanBeCanceled ? finalizationToken : cleanup!.Token).ConfigureAwait(false);
            }
        }
    }

    private async Task<ProjectionSourceGuardDeliveryResult> ReleaseAndBindAsync(AggregateIdentity identity,
        NamedProjectionRouteCatalogEntry catalog, string projection, ProjectionDeliveryFingerprintHistory admitted,
        ProjectionDeliveryReservation reservation, string operation, ProjectionOwnerProof selected,
        CancellationToken cancellationToken)
    {
        if (!await lifecycle.CompleteDeliveryWriteAsync(identity, projection, operation, cancellationToken).ConfigureAwait(false))
            return new(true, false, "delivery_in_progress");
        ProjectionLifecycleSnapshot idle = await lifecycle.ReadSnapshotAsync(identity, projection, cancellationToken).ConfigureAwait(false);
        if (idle.Phase != ProjectionLifecyclePhase.Idle) return new(true, false, "delivery_in_progress");
        ProjectionOwnerProofResult readback = await owners.ReadAsync(identity, catalog, projection, idle, cancellationToken).ConfigureAwait(false);
        if (readback.Proof != selected) return new(true, false, readback.ReasonCode ?? "delivery_reconciliation_required");
        ProjectionDeliveryGuardReservation binding = reservation.GuardBinding!;
        IAggregateActor source = actors.CreateActorProxy<IAggregateActor>(new ActorId(identity.ActorId), actorOptions.Value.AggregateActorTypeName);
        SourceProofInspection current = await source.InspectSourceProofAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
        if (current.Status != SourceProofInspectionStatus.Available || current.Proof is not { } proof
            || proof.SourceIncarnation != binding.AdmittedSource.SourceIncarnation || proof.Erased
            || proof.HeadSequence < binding.AdmittedSource.HeadSequence
            || proof.RetainedFloor - 1 > binding.BaseSource.HeadSequence
            || proof.HeadSequence == binding.AdmittedSource.HeadSequence
                && !SourceProofCodec.SameCompletedPrefix(binding.AdmittedSource, proof))
            return new(true, false, "source_conflict");
        var evidence = new ProjectionDeliveryGuardEvidence(binding.AdmittedSource, catalog.CatalogFingerprint,
            binding.HandlerSemantics, selected, idle.Revision);
        ProjectionDeliveryCompletion completed;
        try
        {
            completed = await coordinator.CompleteHistoryAsync(identity, projection, admitted, reservation,
                cancellationToken, evidence).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (ProjectionDeliveryHistoryValidationException exception) when (exception.Message == "delivery_proof_limit_exceeded") { throw; }
        catch
        {
            // An ambiguous CAS response is resolved only by the same row and proof readback below.
            completed = ProjectionDeliveryCompletion.StateUnavailable;
        }
        ProjectionDeliveryStateReadResult row = await states.ReadAsync(identity, projection, cancellationToken).ConfigureAwait(false);
        ProjectionLifecycleSnapshot after = await lifecycle.ReadSnapshotAsync(identity, projection, cancellationToken).ConfigureAwait(false);
        if (row.State is not { } state || !row.WriterProtocolV2Active || row.SourceGuard is not { } marker
            || !marker.IsValid(identity, projection) || marker.SourceIncarnation != binding.AdmittedSource.SourceIncarnation
            || marker.CatalogFingerprint != catalog.CatalogFingerprint || marker.HandlerSemantics != binding.HandlerSemantics
            || row.Classification != ProjectionDeliveryStateClassification.Current
            || state.GuardEvidence != evidence || state.LastDeliveredSequence != reservation.EndSequence
            || state.CompletedPrefixFingerprint != reservation.ManifestFingerprint
            || state.LastCompletedMessageId != reservation.HeadMessageId
            || state.ActiveReservation is not null || state.IdleBindingPending is not null
            || !ProjectionDeliveryIdempotencyCoordinator.ValidCompleteIdentityDigests(state))
            return new(true, false, completed == ProjectionDeliveryCompletion.Fenced ? "delivery_in_progress" : "delivery_reconciliation_required");
        if (after != idle) return new(true, false, "head_changed") { CompletionProven = true, FinalSourceHeadSequence = proof.HeadSequence };
        SourceProofInspection finalSource;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            finalSource = await source.InspectSourceProofAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        { return new(true, false, "delivery_reconciliation_required") { CompletionProven = true, FinalSourceHeadSequence = proof.HeadSequence }; }
        catch
        { return new(true, false, "delivery_state_unavailable") { CompletionProven = true, FinalSourceHeadSequence = proof.HeadSequence }; }
        if (finalSource.Status != SourceProofInspectionStatus.Available || finalSource.Erased || finalSource.Proof is not { } finalProof
            || finalProof.SourceIncarnation != binding.AdmittedSource.SourceIncarnation
            || finalProof.HeadSequence < binding.AdmittedSource.HeadSequence
            || finalProof.HeadSequence == binding.AdmittedSource.HeadSequence
                && !SourceProofCodec.SameCompletedPrefix(binding.AdmittedSource, finalProof))
            return new(true, false, "source_conflict") { CompletionProven = true, FinalSourceHeadSequence = finalSource.CurrentSequence };
        return new(true, finalProof == binding.AdmittedSource,
            finalProof == binding.AdmittedSource ? null : "head_changed")
            { CompletionProven = true, FinalSourceHeadSequence = finalProof.HeadSequence };
    }
}
