using Hexalith.EventStore.Client.Projections;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Server.Events;

namespace Hexalith.EventStore.Server.Projections;

internal sealed partial class ProjectionDeliveryIdempotencyCoordinator
{
    /// <inheritdoc/>
    public async Task<ProjectionDeliveryReservation?> TrySwitchToCanonicalReplayAsync(AggregateIdentity identity,
        string projectionName, ProjectionDeliveryReservation reservation, CancellationToken cancellationToken)
    {
        if (reservation.GuardBinding is not { CanonicalReplay: false } binding) return null;
        options.Value.Validate();
        for (int attempt = 0; attempt < options.Value.MaxStateTransitionAttempts; attempt++)
        {
            ProjectionDeliveryStateReadResult read = await stateStore.ReadAsync(identity, projectionName, cancellationToken).ConfigureAwait(false);
            if (!read.WriterProtocolV2Active || read.Classification != ProjectionDeliveryStateClassification.Current
                || !GuardMarkerMatches(read.SourceGuard, identity, projectionName, binding)
                || read.State is not { IdleBindingPending: null } state || state.ActiveReservation != reservation
                || state.LastDeliveredSequence != binding.BaseSource.HeadSequence
                || state.GuardEvidence?.OwnerProof != binding.BaseOwnerProof || !ValidCompleteIdentityDigests(state)) return null;
            ProjectionDeliveryReservation replacement = reservation with
            {
                FencingToken = checked(reservation.FencingToken + 1),
                GuardBinding = binding with { CanonicalReplay = true },
            };
            if (await TrySaveAsync(identity, projectionName, state with { ActiveReservation = replacement,
                UpdatedAt = timeProvider.GetUtcNow() }, read.Etag, cancellationToken).ConfigureAwait(false)) return replacement;
        }
        return null;
    }

    /// <inheritdoc/>
    public async Task<ProjectionDeliveryReservation?> TryReclaimIdleBindingAsync(AggregateIdentity identity,
        string projectionName, ProjectionDeliveryReservation reservation, string lifecycleOperationToken,
        CancellationToken cancellationToken = default, long? resumeFencingToken = null)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(reservation);
        ArgumentException.ThrowIfNullOrWhiteSpace(lifecycleOperationToken);
        ProjectionKeySegments.Validate(projectionName, nameof(projectionName));
        options.Value.Validate();
        if (reservation.GuardBinding is not { } binding) return null;
        for (int attempt = 0; attempt < options.Value.MaxStateTransitionAttempts; attempt++)
        {
            ProjectionDeliveryStateReadResult read = await stateStore.ReadAsync(identity, projectionName, cancellationToken).ConfigureAwait(false);
            DateTimeOffset now = timeProvider.GetUtcNow();
            if (!read.WriterProtocolV2Active || read.Classification != ProjectionDeliveryStateClassification.Current
                || !GuardMarkerMatches(read.SourceGuard, identity, projectionName, binding)
                || read.State is not { SchemaVersion: ProjectionDeliveryState.GuardSchemaVersion, GuardEvidenceVersion: 1 } state
                || state.ActiveReservation != reservation || state.IdleBindingPending is not { } pending
                || pending.FencingToken != reservation.FencingToken || pending.AdmittedHead != reservation.EndSequence
                || state.LastDeliveredSequence != binding.BaseSource.HeadSequence
                || !IsValidCurrentState(state, identity, projectionName, ProjectionDeliveryFingerprint.ComputeInitial(identity,
                    projectionName, state.CompletedPrefixFingerprint?.StartsWith("v2:", StringComparison.Ordinal) == true))
                || !ValidCompleteIdentityDigests(state) || !ValidOwnerProof(pending.OwnerProof, identity, projectionName)
                || pending.OwnerProof.Selection.AppliedSequence != reservation.EndSequence
                || pending.OwnerProof.Selection.MessageId != reservation.HeadMessageId
                || pending.OwnerProof.Selection.OwnerGeneration != binding.SelectedOwnerGeneration
                || reservation.ExpiresAt > now && resumeFencingToken != reservation.FencingToken)
                return null;
            ProjectionDeliveryReservation reclaimed = reservation with
            {
                FencingToken = checked(reservation.FencingToken + 1), AdmittedAt = now,
                ExpiresAt = now + options.Value.ReservationLease, Attempt = checked(reservation.Attempt + 1),
            };
            ProjectionDeliveryState replacement = state with
            {
                ActiveReservation = reclaimed,
                IdleBindingPending = pending with { FencingToken = reclaimed.FencingToken, LifecycleOperationToken = lifecycleOperationToken },
                UpdatedAt = now,
            };
            if (await TrySaveAsync(identity, projectionName, replacement, read.Etag, cancellationToken).ConfigureAwait(false)) return reclaimed;
        }

        return null;
    }

    /// <summary>Prepares immutable finalization references without advancing C or clearing its reservation.</summary>
    public async Task<bool> PrepareIdleBindingAsync(AggregateIdentity identity, string projectionName,
        ProjectionDeliveryReservation reservation, string lifecycleOperationToken, ProjectionOwnerProof ownerProof,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(lifecycleOperationToken);
        ProjectionDeliveryGuardReservation? binding = reservation.GuardBinding;
        if (binding is null || !ValidOwnerProof(ownerProof, identity, projectionName)
            || ownerProof.Selection.AppliedSequence != reservation.EndSequence
            || ownerProof.Selection.MessageId != reservation.HeadMessageId
            || ownerProof.Selection.OwnerGeneration != binding.SelectedOwnerGeneration) return false;
        var pending = new ProjectionDeliveryIdleBindingPending(reservation.FencingToken,
            reservation.EndSequence, lifecycleOperationToken, ownerProof);
        for (int attempt = 0; attempt < options.Value.MaxStateTransitionAttempts; attempt++)
        {
            ProjectionDeliveryStateReadResult read = await stateStore.ReadAsync(identity, projectionName, cancellationToken).ConfigureAwait(false);
            if (!read.WriterProtocolV2Active || read.Classification != ProjectionDeliveryStateClassification.Current
                || !GuardMarkerMatches(read.SourceGuard, identity, projectionName, binding)
                || read.State is not { SchemaVersion: ProjectionDeliveryState.GuardSchemaVersion, GuardEvidenceVersion: 1 } state
                || state.ActiveReservation != reservation || state.LastDeliveredSequence != binding.BaseSource.HeadSequence)
                return false;
            if (state.IdleBindingPending is not null) return state.IdleBindingPending == pending;
            if (await TrySaveAsync(identity, projectionName, state with { IdleBindingPending = pending,
                UpdatedAt = timeProvider.GetUtcNow() }, read.Etag, cancellationToken).ConfigureAwait(false)) return true;
        }

        return false;
    }

    private static bool ValidateGuardAdmission(ProjectionDeliveryStateReadResult read, AggregateIdentity identity,
        string projectionName, ProjectionDeliveryFingerprintHistory history, ProjectionDeliveryGuardReservation binding)
    {
        if (!GuardMarkerMatches(read.SourceGuard, identity, projectionName, binding)
            || read.State is not { SchemaVersion: ProjectionDeliveryState.GuardSchemaVersion, GuardEvidenceVersion: 1 } state
            || state.LastDeliveredSequence != binding.BaseSource.HeadSequence
            || history.BaseSequence != state.LastDeliveredSequence
            || binding.AdmittedSource.HeadSequence <= state.LastDeliveredSequence
            || binding.AdmittedSource.HeadSequence > 10_000
            || binding.AdmittedSource.RetainedFloor - 1 > state.LastDeliveredSequence
            || !SourceProofCodec.IsValid(binding.BaseSource, identity, binding.BaseSource.HeadSequence, binding.BaseSource.RetainedFloor)
            || !SourceProofCodec.IsValid(binding.AdmittedSource, identity, binding.AdmittedSource.HeadSequence, binding.AdmittedSource.RetainedFloor)
            || binding.BaseSource.SourceIncarnation != binding.AdmittedSource.SourceIncarnation
            || binding.BaseSource.Erased || binding.AdmittedSource.Erased
            || history.Events[^1].SequenceNumber != binding.AdmittedSource.HeadSequence
            || history.Events[^1].MessageId != binding.AdmittedSource.HeadMessageId
            || string.IsNullOrWhiteSpace(binding.SelectedOwnerGeneration)
            || !ValidCompleteIdentityDigests(state)) return false;
        if (state.LastDeliveredSequence == 0)
            return binding.BaseOwnerProof is null && state.GuardEvidence is null
                && binding.BaseSource == SourceProofCodec.CreateSeed(identity, binding.BaseSource.SourceIncarnation);
        return state.GuardEvidence is { } evidence && binding.BaseOwnerProof == evidence.OwnerProof
            && evidence.Source.CumulativeDigest == binding.BaseSource.CumulativeDigest
            && evidence.Source.HeadMessageId == binding.BaseSource.HeadMessageId
            && evidence.Source.SourceIncarnation == binding.BaseSource.SourceIncarnation
            && evidence.CatalogFingerprint == binding.CatalogFingerprint && evidence.HandlerSemantics == binding.HandlerSemantics
            && binding.BaseOwnerProof.Selection.AppliedSequence == state.LastDeliveredSequence
            && binding.BaseOwnerProof.Selection.OwnerGeneration == binding.SelectedOwnerGeneration;
    }

    private static bool ValidateGuardCompletion(ProjectionDeliveryStateReadResult read, AggregateIdentity identity,
        string projectionName, ProjectionDeliveryReservation reservation, ProjectionDeliveryGuardEvidence evidence)
    {
        ProjectionDeliveryGuardReservation? binding = reservation.GuardBinding;
        return binding is not null && GuardMarkerMatches(read.SourceGuard, identity, projectionName, binding)
            && read.State is { SchemaVersion: ProjectionDeliveryState.GuardSchemaVersion, GuardEvidenceVersion: 1 } state
            && state.IdleBindingPending is { } pending && pending.FencingToken == reservation.FencingToken
            && pending.AdmittedHead == reservation.EndSequence && !string.IsNullOrWhiteSpace(pending.LifecycleOperationToken)
            && state.ActiveReservation == reservation && state.LastDeliveredSequence == binding.BaseSource.HeadSequence
            && evidence.Source == binding.AdmittedSource && evidence.CatalogFingerprint == binding.CatalogFingerprint
            && evidence.HandlerSemantics == binding.HandlerSemantics && evidence.IdleLifecycleRevision > 0
            && evidence.OwnerProof == pending.OwnerProof && evidence.OwnerProof.Selection.AppliedSequence == reservation.EndSequence
            && ValidOwnerProof(evidence.OwnerProof, identity, projectionName)
            && evidence.OwnerProof.Selection.MessageId == reservation.HeadMessageId
            && evidence.OwnerProof.Selection.OwnerGeneration == binding.SelectedOwnerGeneration
            && ValidCompleteIdentityDigests(state);
    }

    private static bool GuardMarkerMatches(ProjectionSourceGuard? marker, AggregateIdentity identity,
        string projectionName, ProjectionDeliveryGuardReservation binding)
        => marker is not null && marker.IsValid(identity, projectionName)
            && marker.SourceIncarnation == binding.AdmittedSource.SourceIncarnation
            && marker.CatalogFingerprint == binding.CatalogFingerprint && marker.HandlerSemantics == binding.HandlerSemantics;

    internal static bool ValidCompleteIdentityDigests(ProjectionDeliveryState state)
    {
        if (state.CompletedMessageIdDigests?.Count != state.LastDeliveredSequence) return false;
        if (ProjectionMessageIdDigests.CreateSet(state.CompletedMessageIdDigests) is null) return false;

        if (!ProjectionDeliveryFingerprint.IsCanonical(state.CompletedPrefixFingerprint)) return false;
        foreach (ProjectionDeliveryReceipt receipt in ProjectionDeliveryCostDiagnostics.VisitProof(state.CompletedReceipts ?? []))
        {
            if (receipt.SequenceNumber <= 0 || receipt.SequenceNumber > state.CompletedMessageIdDigests.Count
                || !ProjectionDeliveryFingerprint.IsCanonical(receipt.EventFingerprint)
                || !ProjectionDeliveryFingerprint.IsCanonical(receipt.PrefixFingerprint)
                || state.CompletedMessageIdDigests[checked((int)receipt.SequenceNumber - 1)] != MessageIdentityDigest(receipt.MessageId))
                return false;
        }

        return true;
    }

    internal static bool ValidOwnerProof(ProjectionOwnerProof proof, AggregateIdentity identity, string projection)
        => proof.Selection.Version == 1 && proof.Selection.TenantId == identity.TenantId
            && proof.Selection.Domain == identity.Domain && proof.Selection.AggregateId == identity.AggregateId
            && proof.Selection.ProjectionName == projection && proof.Selection.AppliedSequence > 0
            && ReadModelBatchFingerprint.IsCanonical(proof.ReceiptFingerprint)
            && new[] { proof.Selection.OwnerIdentity, proof.Selection.OwnerGeneration, proof.Selection.MessageId,
                proof.Selection.BatchId, proof.Selection.FreshnessVersion, proof.ReceiptIdentity, proof.ReceiptFingerprint }
                .All(static value => !string.IsNullOrWhiteSpace(value));
}
