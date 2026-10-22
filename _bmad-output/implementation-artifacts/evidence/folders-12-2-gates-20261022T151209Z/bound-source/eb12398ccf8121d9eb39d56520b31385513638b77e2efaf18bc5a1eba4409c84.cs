using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Server.Configuration;

using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>ETag-authoritative projection delivery admission and completion state machine.</summary>
internal sealed partial class ProjectionDeliveryIdempotencyCoordinator(
    IProjectionDeliveryStateStore stateStore,
    IOptions<ProjectionDeliveryIdempotencyOptions> options,
    TimeProvider timeProvider) : IProjectionDeliveryIdempotencyCoordinator {
    /// <inheritdoc/>
    public async Task<ProjectionDeliveryAcceptedPrefix?> ReadAcceptedPrefixAsync(
        AggregateIdentity identity, string projectionName, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(identity);
        ProjectionKeySegments.Validate(projectionName, nameof(projectionName));
        ProjectionDeliveryStateReadResult read = await stateStore.ReadAsync(identity, projectionName,
            cancellationToken).ConfigureAwait(false);
        ProjectionDeliveryState? state = read.State;
        if (!read.WriterProtocolV2Active
            || read.Classification != ProjectionDeliveryStateClassification.Current
            || state is null || state.LastDeliveredSequence <= 0
            || string.IsNullOrWhiteSpace(state.CompletedPrefixFingerprint)) return null;
        bool positionSensitive = state.CompletedPrefixFingerprint.StartsWith("v2:", StringComparison.Ordinal);
        string initial = ProjectionDeliveryFingerprint.ComputeInitial(identity, projectionName,
            positionSensitive);
        return IsValidCurrentState(state, identity, projectionName, initial)
            && state.CompletedMessageIdDigests?.Count == state.LastDeliveredSequence
            ? new(state.LastDeliveredSequence, state.CompletedPrefixFingerprint) : null;
    }

    /// <inheritdoc/>
    public async Task<bool> UseLegacyMappedIdentityAsync(AggregateIdentity identity,
        string projectionName, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(identity);
        ProjectionKeySegments.Validate(projectionName, nameof(projectionName));
        ProjectionDeliveryStateReadResult read = await stateStore.ReadAsync(identity, projectionName,
            cancellationToken).ConfigureAwait(false);
        return read.State?.CompletedPrefixFingerprint?.StartsWith("v1:", StringComparison.Ordinal) == true;
    }

    /// <inheritdoc/>
    public async Task<ProjectionDeliveryAdmissionResult> TryAdmitAsync(
        AggregateIdentity identity,
        string projectionName,
        IReadOnlyList<ProjectionEventDto> events,
        bool reclaimSafe,
        CancellationToken cancellationToken = default,
        long? resumeFencingToken = null) {
        ArgumentNullException.ThrowIfNull(identity);
        ProjectionKeySegments.Validate(projectionName, nameof(projectionName));
        ProjectionDeliveryAdmissionResult? malformed = ValidateInput(events);
        if (malformed is not null) {
            return malformed;
        }

        ProjectionDeliveryFingerprintHistory history;
        try {
            history = ProjectionDeliveryFingerprint.ComputeHistory(identity, projectionName, events);
        }
        catch (ArgumentException) {
            return Failed(ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
        }

        return await TryAdmitHistoryAsync(identity, projectionName, history, reclaimSafe,
            cancellationToken, resumeFencingToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<ProjectionDeliveryAdmissionResult> TryAdmitHistoryAsync(
        AggregateIdentity identity,
        string projectionName,
        ProjectionDeliveryFingerprintHistory history,
        bool reclaimSafe,
        CancellationToken cancellationToken = default,
        long? resumeFencingToken = null,
        ProjectionDeliveryGuardReservation? guardBinding = null) {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(history);
        ProjectionKeySegments.Validate(projectionName, nameof(projectionName));
        if (!ProjectionDeliveryFingerprint.IsValidHistory(identity, projectionName, history)) {
            return Failed(ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
        }

        ProjectionDeliveryIdempotencyOptions configured = options.Value;
        configured.Validate();
        ProjectionDeliveryEventDigest head = history.Events[^1];
        for (int attempt = 0; attempt < configured.MaxStateTransitionAttempts; attempt++) {
            cancellationToken.ThrowIfCancellationRequested();
            ProjectionDeliveryStateReadResult read;
            try {
                read = await stateStore.ReadAsync(identity, projectionName, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) {
                throw;
            }
            catch {
                return Retryable(ProjectionDispatchReasonCodes.DeliveryStateUnavailable);
            }

            if (!read.WriterProtocolV2Active) {
                return Retryable(ProjectionDispatchReasonCodes.DeliveryStateUnavailable);
            }

            if (guardBinding is null && (read.SourceGuard is not null || read.State?.SchemaVersion == ProjectionDeliveryState.GuardSchemaVersion))
                return Retryable("guard_activation_required");
            if (guardBinding is not null && !ValidateGuardAdmission(read, identity, projectionName, history, guardBinding))
                return Failed(ProjectionDispatchReasonCodes.DeliveryReconciliationRequired);

            if (read.Classification is ProjectionDeliveryStateClassification.SchemaRegression
                or ProjectionDeliveryStateClassification.Unsupported) {
                return await ReconciliationFailureAsync(
                    identity,
                    projectionName,
                    head.SequenceNumber,
                    read.State?.SchemaVersion ?? 0,
                    ProjectionDispatchReasonCodes.DeliverySchemaRegression,
                    cancellationToken).ConfigureAwait(false);
            }

            if (read.Classification is ProjectionDeliveryStateClassification.LegacyNonZero
                or ProjectionDeliveryStateClassification.LegacyZero) {
                return await ReconciliationFailureAsync(
                    identity,
                    projectionName,
                    head.SequenceNumber,
                    read.State?.SchemaVersion ?? 0,
                    ProjectionDispatchReasonCodes.DeliveryReconciliationRequired,
                    cancellationToken).ConfigureAwait(false);
            }

            DateTimeOffset now = timeProvider.GetUtcNow();
            ProjectionDeliveryState current = read.State
                ?? ProjectionDeliveryState.CreateEmpty(identity, projectionName, history.InitialFingerprint, now);
            if (!IsValidCurrentState(
                current,
                identity,
                projectionName,
                history.InitialFingerprint)) {
                return await ReconciliationFailureAsync(
                    identity,
                    projectionName,
                    head.SequenceNumber,
                    current.SchemaVersion,
                    ProjectionDispatchReasonCodes.DeliverySchemaRegression,
                    cancellationToken).ConfigureAwait(false);
            }

            ProjectionDeliveryAdmissionResult? completed = ClassifyCompletedOrConflict(current, history);
            if (completed is not null) {
                if (string.Equals(
                    completed.ReasonCode,
                    ProjectionDispatchReasonCodes.DeliveryReconciliationRequired,
                    StringComparison.Ordinal)) {
                    return await ReconciliationFailureAsync(
                        identity,
                        projectionName,
                        head.SequenceNumber,
                        current.SchemaVersion,
                        ProjectionDispatchReasonCodes.DeliveryReconciliationRequired,
                        cancellationToken).ConfigureAwait(false);
                }

                return completed;
            }

            if (history.BaseSequence > 0
                && (current.LastDeliveredSequence != history.BaseSequence
                    || !string.Equals(current.CompletedPrefixFingerprint, history.BaseFingerprint,
                        StringComparison.Ordinal))) {
                return Failed(ProjectionDispatchReasonCodes.DeliveryGap);
            }

            if (history.BaseSequence > 0
                && current.CompletedMessageIdDigests?.Count != history.BaseSequence) {
                return Failed(ProjectionDispatchReasonCodes.DeliveryReconciliationRequired);
            }

            ProjectionDeliveryAdmissionResult? prefixConflict = ValidateCompletedPrefix(current, history);
            if (prefixConflict is not null) {
                return prefixConflict;
            }

            ProjectionDeliveryReservation? active = current.ActiveReservation;
            if (active is not null) {
                if (current.IdleBindingPending is not null) return Retryable(ProjectionDispatchReasonCodes.DeliveryInProgress);
                if (active.GuardBinding != guardBinding) return Failed(ProjectionDispatchReasonCodes.DeliveryReconciliationRequired);
                bool sameIdentity = active.EndSequence == head.SequenceNumber
                    && string.Equals(active.HeadMessageId, head.MessageId, StringComparison.Ordinal);
                if (!sameIdentity) {
                    return Retryable(ProjectionDispatchReasonCodes.DeliveryInProgress);
                }

                if (!string.Equals(active.DispatchId, head.MessageId, StringComparison.Ordinal)
                    || !string.Equals(active.ManifestFingerprint, history.PrefixFingerprint, StringComparison.Ordinal)) {
                    return Failed(ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
                }

                // The durable retry work item carries the fencing token of the reservation it created.
                // A caller presenting that exact token is the reservation's rightful owner resuming its
                // own interrupted delivery after a transient handler failure, so it may reclaim
                // immediately instead of waiting out the lease. Any caller without the matching token must
                // wait for the lease to lapse before a safe reclaim, so a stale or competing writer can
                // never cut in front of the live owner. Reclaim always advances the fencing token, so a
                // same-token race is resolved by the ETag save below: exactly one reclaim wins and the
                // loser re-reads an advanced token and is fenced back onto this wait path.
                bool ownsActiveReservation = resumeFencingToken is { } presentedToken
                    && active.FencingToken > 0
                    && presentedToken == active.FencingToken;
                if (active.ExpiresAt > now && !ownsActiveReservation) {
                    return Retryable(ProjectionDispatchReasonCodes.DeliveryInProgress);
                }

                if (!reclaimSafe) {
                    return await ReconciliationFailureAsync(
                        identity,
                        projectionName,
                        head.SequenceNumber,
                        current.SchemaVersion,
                        ProjectionDispatchReasonCodes.DeliveryReconciliationRequired,
                        cancellationToken).ConfigureAwait(false);
                }

                var reclaimed = active with {
                    FencingToken = checked(active.FencingToken + 1),
                    AdmittedAt = now,
                    ExpiresAt = now + configured.ReservationLease,
                    Attempt = checked(active.Attempt + 1),
                };
                ProjectionDeliveryState reclaimedState = current with {
                    ActiveReservation = reclaimed,
                    UpdatedAt = now,
                };
                if (await TrySaveAsync(identity, projectionName, reclaimedState, read.Etag, cancellationToken).ConfigureAwait(false)) {
                    return new ProjectionDeliveryAdmissionResult(
                        ProjectionDeliveryAdmissionDisposition.Dispatch,
                        ProjectionDispatchReasonCodes.DeliveryLeaseReclaimed,
                        reclaimed);
                }

                continue;
            }

            if (HasMessageIdentityConflict(current, history)) {
                return Failed(ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
            }

            var reservation = new ProjectionDeliveryReservation(
                current.LastDeliveredSequence + 1,
                head.SequenceNumber,
                head.MessageId,
                head.MessageId,
                history.PrefixFingerprint,
                1,
                now,
                now + configured.ReservationLease,
                1) { GuardBinding = guardBinding };
            ProjectionDeliveryState reserved = current with {
                ActiveReservation = reservation,
                UpdatedAt = now,
            };
            if (await TrySaveAsync(identity, projectionName, reserved, read.Etag, cancellationToken).ConfigureAwait(false)) {
                return new ProjectionDeliveryAdmissionResult(ProjectionDeliveryAdmissionDisposition.Dispatch, null, reservation);
            }
        }

        return Retryable(ProjectionDispatchReasonCodes.DeliveryStateUnavailable);
    }

    /// <inheritdoc/>
    public async Task<ProjectionDeliveryCompletion> CompleteAsync(
        AggregateIdentity identity,
        string projectionName,
        IReadOnlyList<ProjectionEventDto> events,
        ProjectionDeliveryReservation reservation,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(reservation);
        ProjectionKeySegments.Validate(projectionName, nameof(projectionName));
        ProjectionDeliveryFingerprintHistory history;
        try {
            history = ProjectionDeliveryFingerprint.ComputeHistory(identity, projectionName, events);
        }
        catch (ArgumentException) {
            return ProjectionDeliveryCompletion.Fenced;
        }

        return await CompleteHistoryAsync(identity, projectionName, history, reservation,
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<ProjectionDeliveryCompletion> CompleteHistoryAsync(
        AggregateIdentity identity,
        string projectionName,
        ProjectionDeliveryFingerprintHistory history,
        ProjectionDeliveryReservation reservation,
        CancellationToken cancellationToken = default,
        ProjectionDeliveryGuardEvidence? guardEvidence = null) {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(history);
        ArgumentNullException.ThrowIfNull(reservation);
        ProjectionKeySegments.Validate(projectionName, nameof(projectionName));
        if (!ProjectionDeliveryFingerprint.IsValidHistory(identity, projectionName, history)) {
            return ProjectionDeliveryCompletion.Fenced;
        }

        if (history.Events[^1].SequenceNumber != reservation.EndSequence
            || !string.Equals(history.Events[^1].MessageId, reservation.HeadMessageId, StringComparison.Ordinal)
            || !string.Equals(history.PrefixFingerprint, reservation.ManifestFingerprint, StringComparison.Ordinal)
            || !string.Equals(reservation.DispatchId, reservation.HeadMessageId, StringComparison.Ordinal)) {
            return ProjectionDeliveryCompletion.Fenced;
        }

        ProjectionDeliveryIdempotencyOptions configured = options.Value;
        configured.Validate();
        for (int attempt = 0; attempt < configured.MaxStateTransitionAttempts; attempt++) {
            cancellationToken.ThrowIfCancellationRequested();
            ProjectionDeliveryStateReadResult read;
            try {
                read = await stateStore.ReadAsync(identity, projectionName, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) {
                throw;
            }
            catch {
                return ProjectionDeliveryCompletion.StateUnavailable;
            }

            if (!read.WriterProtocolV2Active
                || read.Classification != ProjectionDeliveryStateClassification.Current
                || read.State is null) {
                return ProjectionDeliveryCompletion.StateUnavailable;
            }

            ProjectionDeliveryState current = read.State;
            if (guardEvidence is null && (read.SourceGuard is not null || current.SchemaVersion == ProjectionDeliveryState.GuardSchemaVersion))
                return ProjectionDeliveryCompletion.Fenced;
            if (!IsValidCurrentState(
                current,
                identity,
                projectionName,
                history.InitialFingerprint)) {
                return ProjectionDeliveryCompletion.Fenced;
            }

            if (IsExactCompleted(current, history.Events[^1])) {
                if (guardEvidence is not null && (current.GuardEvidence != guardEvidence
                    || current.ActiveReservation is not null || current.IdleBindingPending is not null
                    || reservation.GuardBinding is not { } completedBinding
                    || !GuardMarkerMatches(read.SourceGuard, identity, projectionName, completedBinding)
                    || !ValidCompleteIdentityDigests(current))) return ProjectionDeliveryCompletion.Fenced;
                return ProjectionDeliveryCompletion.AlreadyCompleted;
            }

            if (guardEvidence is not null && !ValidateGuardCompletion(read, identity, projectionName, reservation, guardEvidence))
                return ProjectionDeliveryCompletion.Fenced;

            if (history.BaseSequence > 0
                && (current.LastDeliveredSequence != history.BaseSequence
                    || !string.Equals(current.CompletedPrefixFingerprint, history.BaseFingerprint,
                        StringComparison.Ordinal))) {
                return ProjectionDeliveryCompletion.Fenced;
            }

            if (history.BaseSequence > 0
                && current.CompletedMessageIdDigests?.Count != history.BaseSequence) {
                return ProjectionDeliveryCompletion.Fenced;
            }

            if (current.ActiveReservation != reservation
                || current.LastDeliveredSequence != reservation.StartSequence - 1
                || !PrefixMatches(current, history)) {
                return ProjectionDeliveryCompletion.Fenced;
            }

            long retainedFloor = Math.Max(current.FirstRetainedSequence,
                Math.Max(1, reservation.EndSequence - configured.CompletedReceiptLimit + 1));
            var retained = new List<ProjectionDeliveryReceipt>(configured.CompletedReceiptLimit);
            foreach (ProjectionDeliveryReceipt receipt in ProjectionDeliveryCostDiagnostics.VisitProof(current.CompletedReceipts ?? []))
                if (receipt.SequenceNumber >= retainedFloor) retained.Add(receipt);
            long appendedCount = 0;
            foreach (ProjectionDeliveryEventDigest value in ProjectionDeliveryCostDiagnostics.VisitProof(history.Events))
            {
                if (value.SequenceNumber < reservation.StartSequence || value.SequenceNumber > reservation.EndSequence) continue;
                appendedCount++;
                // All supplied identities still participate in validation and
                // the complete digest list. Only exact receipts within the
                // configured existing horizon need a retained object/string.
                if (value.SequenceNumber >= retainedFloor)
                    retained.Add(new(value.SequenceNumber, value.MessageId, value.EventFingerprint, value.PrefixFingerprint));
            }
            if (appendedCount != reservation.EndSequence - reservation.StartSequence + 1) {
                return ProjectionDeliveryCompletion.Fenced;
            }

            ProjectionDeliveryReceipt[] compacted = [.. retained];
            ProjectionDeliveryEventDigest completedHead = history.Events[^1];
            ProjectionDeliveryState completed = current with {
                LastDeliveredSequence = completedHead.SequenceNumber,
                CompletedMessageIdDigests = ProjectionMessageIdDigests.Append(
                    history.BaseSequence == 0 ? null : current.CompletedMessageIdDigests, history.Events),
                LastCompletedMessageId = completedHead.MessageId,
                CompletedPrefixFingerprint = completedHead.PrefixFingerprint,
                CompletedReceipts = compacted,
                FirstRetainedSequence = compacted[0].SequenceNumber,
                ActiveReservation = null,
                IdleBindingPending = null,
                GuardEvidenceVersion = guardEvidence is null ? current.GuardEvidenceVersion : 1,
                GuardEvidence = guardEvidence ?? current.GuardEvidence,
                UpdatedAt = timeProvider.GetUtcNow(),
            };
            if (await TrySaveAsync(identity, projectionName, completed, read.Etag, cancellationToken).ConfigureAwait(false)) {
                return ProjectionDeliveryCompletion.Completed;
            }
        }

        return ProjectionDeliveryCompletion.StateUnavailable;
    }

    /// <inheritdoc/>
    public async Task<bool> TryReleaseAsync(
        AggregateIdentity identity,
        string projectionName,
        ProjectionDeliveryReservation reservation,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(reservation);
        ProjectionKeySegments.Validate(projectionName, nameof(projectionName));
        ProjectionDeliveryIdempotencyOptions configured = options.Value;
        configured.Validate();
        for (int attempt = 0; attempt < configured.MaxStateTransitionAttempts; attempt++) {
            cancellationToken.ThrowIfCancellationRequested();
            ProjectionDeliveryStateReadResult read;
            try {
                read = await stateStore.ReadAsync(identity, projectionName, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) {
                throw;
            }
            catch {
                return false;
            }

            if (!read.WriterProtocolV2Active
                || read.Classification != ProjectionDeliveryStateClassification.Current
                || read.State?.ActiveReservation != reservation || read.State.IdleBindingPending is not null) {
                return false;
            }

            ProjectionDeliveryState released = read.State with {
                ActiveReservation = null,
                UpdatedAt = timeProvider.GetUtcNow(),
            };
            if (await TrySaveAsync(identity, projectionName, released, read.Etag, cancellationToken).ConfigureAwait(false)) {
                return true;
            }
        }

        return false;
    }

    private static ProjectionDeliveryAdmissionResult? ValidateInput(IReadOnlyList<ProjectionEventDto> events) {
        if (events is null || events.Count == 0) {
            return Failed(ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
        }

        long expected = 1;
        foreach (ProjectionEventDto value in events) {
            if (value is null || value.SequenceNumber <= 0 || string.IsNullOrWhiteSpace(value.MessageId)) {
                return Failed(ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
            }

            if (value.SequenceNumber > expected) {
                return Retryable(ProjectionDispatchReasonCodes.DeliveryGap);
            }

            if (value.SequenceNumber < expected) {
                return Failed(ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
            }

            expected++;
        }

        return null;
    }

    internal static bool IsValidCurrentState(
        ProjectionDeliveryState state,
        AggregateIdentity identity,
        string projectionName,
        string initialFingerprint) {
        if ((state.SchemaVersion != ProjectionDeliveryState.CurrentSchemaVersion
                && state.SchemaVersion != ProjectionDeliveryState.GuardSchemaVersion)
            || state.WriterProtocolVersion != ProjectionDeliveryState.CurrentWriterProtocolVersion
            || !string.Equals(state.TenantId, identity.TenantId, StringComparison.Ordinal)
            || !string.Equals(state.Domain, identity.Domain, StringComparison.Ordinal)
            || !string.Equals(state.AggregateId, identity.AggregateId, StringComparison.Ordinal)
            || !string.Equals(state.ProjectionName, projectionName, StringComparison.Ordinal)
            || state.LastDeliveredSequence < 0
            || state.CompletedReceipts is null
            || state.CompletedReceipts.Count > ProjectionDeliveryIdempotencyOptions.MaximumCompletedReceiptLimit
            || state.UpdatedAt == default
            || string.IsNullOrWhiteSpace(state.CompletedPrefixFingerprint)) {
            return false;
        }

        if (!IsValidReservation(state)) {
            return false;
        }

        if (state.LastDeliveredSequence == 0) {
            return state.LastCompletedMessageId is null
                && state.CompletedReceipts.Count == 0
                && state.FirstRetainedSequence == 0
                && string.Equals(state.CompletedPrefixFingerprint, initialFingerprint, StringComparison.Ordinal);
        }

        if (state.CompletedReceipts.Count == 0
            || state.FirstRetainedSequence <= 0
            || state.FirstRetainedSequence > state.LastDeliveredSequence) {
            return false;
        }

        var messageIds = new HashSet<string>(StringComparer.Ordinal);
        long expectedSequence = state.FirstRetainedSequence;
        foreach (ProjectionDeliveryReceipt receipt in ProjectionDeliveryCostDiagnostics.VisitProof(state.CompletedReceipts)) {
            if (receipt is null
                || receipt.SequenceNumber != expectedSequence
                || receipt.SequenceNumber <= 0
                || string.IsNullOrWhiteSpace(receipt.MessageId)
                || string.IsNullOrWhiteSpace(receipt.EventFingerprint)
                || string.IsNullOrWhiteSpace(receipt.PrefixFingerprint)
                || !messageIds.Add(receipt.MessageId)) {
                return false;
            }

            expectedSequence++;
        }

        ProjectionDeliveryReceipt last = state.CompletedReceipts[^1];
        return last.SequenceNumber == state.LastDeliveredSequence
            && string.Equals(last.MessageId, state.LastCompletedMessageId, StringComparison.Ordinal)
            && string.Equals(last.PrefixFingerprint, state.CompletedPrefixFingerprint, StringComparison.Ordinal);
    }

    private static bool IsValidReservation(ProjectionDeliveryState state) {
        ProjectionDeliveryReservation? reservation = state.ActiveReservation;
        if (reservation is null) {
            return true;
        }

        return state.LastDeliveredSequence < long.MaxValue
            && reservation.StartSequence == state.LastDeliveredSequence + 1
            && reservation.EndSequence >= reservation.StartSequence
            && !string.IsNullOrWhiteSpace(reservation.HeadMessageId)
            && !string.IsNullOrWhiteSpace(reservation.DispatchId)
            && string.Equals(reservation.DispatchId, reservation.HeadMessageId, StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(reservation.ManifestFingerprint)
            && reservation.FencingToken > 0
            && reservation.Attempt > 0
            && reservation.AdmittedAt != default
            && reservation.ExpiresAt > reservation.AdmittedAt
            && state.UpdatedAt >= reservation.AdmittedAt;
    }

    private static ProjectionDeliveryAdmissionResult? ClassifyCompletedOrConflict(
        ProjectionDeliveryState state,
        ProjectionDeliveryFingerprintHistory history) {
        ProjectionDeliveryEventDigest head = history.Events[^1];
        if (head.SequenceNumber > state.LastDeliveredSequence) {
            return null;
        }

        ProjectionDeliveryReceipt? receipt = ProjectionDeliveryCostDiagnostics.VisitProof(state.CompletedReceipts!)
            .FirstOrDefault(value => value.SequenceNumber == head.SequenceNumber);
        if (receipt is null) {
            return Failed(ProjectionDispatchReasonCodes.DeliveryReconciliationRequired);
        }

        if (!ReceiptMatches(receipt, head)) {
            return Failed(ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
        }

        foreach (ProjectionDeliveryReceipt overlap in ProjectionDeliveryCostDiagnostics.VisitProof(state.CompletedReceipts!)
            .Where(value => value.SequenceNumber > history.BaseSequence
                && value.SequenceNumber <= head.SequenceNumber)) {
            ProjectionDeliveryEventDigest supplied = history.Events[
                checked((int)(overlap.SequenceNumber - history.BaseSequence - 1))];
            if (!ReceiptMatches(overlap, supplied)) {
                return Failed(ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
            }
        }

        return new ProjectionDeliveryAdmissionResult(
            ProjectionDeliveryAdmissionDisposition.AlreadyCompleted,
            ProjectionDispatchReasonCodes.DeliveryAlreadyCompleted,
            null);
    }

    private static ProjectionDeliveryAdmissionResult? ValidateCompletedPrefix(
        ProjectionDeliveryState state,
        ProjectionDeliveryFingerprintHistory history) {
        if (state.LastDeliveredSequence >= history.Events[^1].SequenceNumber) {
            return Failed(ProjectionDispatchReasonCodes.DeliveryGap);
        }

        if (!PrefixMatches(state, history)) {
            return Failed(ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
        }

        foreach (ProjectionDeliveryReceipt receipt in ProjectionDeliveryCostDiagnostics.VisitProof(state.CompletedReceipts!)) {
            if (receipt.SequenceNumber <= history.BaseSequence) continue;
            if (receipt.SequenceNumber > history.Events[^1].SequenceNumber
                || !ReceiptMatches(receipt, history.Events[
                    checked((int)(receipt.SequenceNumber - history.BaseSequence - 1))])) {
                return Failed(ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
            }
        }

        return null;
    }

    private static bool PrefixMatches(
        ProjectionDeliveryState state,
        ProjectionDeliveryFingerprintHistory history) =>
        history.BaseSequence > 0
            ? state.LastDeliveredSequence == history.BaseSequence
                && string.Equals(state.CompletedPrefixFingerprint, history.BaseFingerprint,
                    StringComparison.Ordinal)
            : state.LastDeliveredSequence == 0
            ? string.Equals(state.CompletedPrefixFingerprint, history.InitialFingerprint, StringComparison.Ordinal)
            : state.LastDeliveredSequence <= history.Events.Count
                && string.Equals(
                    state.CompletedPrefixFingerprint,
                    history.Events[checked((int)state.LastDeliveredSequence - 1)].PrefixFingerprint,
                    StringComparison.Ordinal);

    private static bool HasMessageIdentityConflict(
        ProjectionDeliveryState state,
        ProjectionDeliveryFingerprintHistory history) {
        var receipts = ProjectionDeliveryCostDiagnostics.VisitProof(state.CompletedReceipts!)
            .ToDictionary(static receipt => receipt.MessageId, StringComparer.Ordinal);
        HashSet<ProjectionMessageIdDigest>? earlier = null;
        if (history.BaseSequence > 0 && state.CompletedMessageIdDigests is { } complete)
        {
            earlier = ProjectionMessageIdDigests.CreateSet(complete);
            if (earlier is null) return true;
        }
        // The admitted table materializes the historical record shape only for
        // a visit. Retaining all supplied records here would duplicate its
        // entire event/prefix proof; index the bounded existing receipts instead.
        foreach (ProjectionDeliveryEventDigest value in ProjectionDeliveryCostDiagnostics.VisitProof(history.Events))
        {
            if (earlier?.Contains(ProjectionMessageIdDigest.FromMessageId(value.MessageId)) == true
                || receipts.TryGetValue(value.MessageId, out ProjectionDeliveryReceipt? receipt)
                    && (value.SequenceNumber != receipt.SequenceNumber || !ReceiptMatches(receipt, value))) return true;
        }
        return false;
    }

    private static string MessageIdentityDigest(string messageId)
        => Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(messageId)));

    private static bool IsExactCompleted(ProjectionDeliveryState state, ProjectionDeliveryEventDigest head) =>
        state.LastDeliveredSequence >= head.SequenceNumber
        && ProjectionDeliveryCostDiagnostics.VisitProof(state.CompletedReceipts ?? []).FirstOrDefault(value => value.SequenceNumber == head.SequenceNumber) is { } receipt
        && ReceiptMatches(receipt, head);

    private static bool ReceiptMatches(ProjectionDeliveryReceipt receipt, ProjectionDeliveryEventDigest value) =>
        receipt.SequenceNumber == value.SequenceNumber
        && string.Equals(receipt.MessageId, value.MessageId, StringComparison.Ordinal)
        && string.Equals(receipt.EventFingerprint, value.EventFingerprint, StringComparison.Ordinal)
        && string.Equals(receipt.PrefixFingerprint, value.PrefixFingerprint, StringComparison.Ordinal);

    private async Task<bool> TrySaveAsync(
        AggregateIdentity identity,
        string projectionName,
        ProjectionDeliveryState state,
        string etag,
        CancellationToken cancellationToken) {
        try {
            return await stateStore.TrySaveAsync(identity, projectionName, state, etag, cancellationToken).ConfigureAwait(false);
        }
        catch (ProjectionDeliveryHistoryValidationException exception) when (exception.Message == "delivery_proof_limit_exceeded") {
            throw;
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch {
            return false;
        }
    }

    private async Task<ProjectionDeliveryAdmissionResult> ReconciliationFailureAsync(
        AggregateIdentity identity,
        string projectionName,
        long observedSequence,
        int stateVersion,
        string reasonCode,
        CancellationToken cancellationToken) {
        try {
            await stateStore.RecordReconciliationAsync(
                identity,
                projectionName,
                new ProjectionDeliveryReconciliationWork(
                    identity.TenantId,
                    identity.Domain,
                    identity.AggregateId,
                    projectionName,
                    reasonCode,
                    observedSequence,
                    stateVersion,
                    null,
                    timeProvider.GetUtcNow()),
                cancellationToken).ConfigureAwait(false);
            return Failed(reasonCode);
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch {
            return Retryable(ProjectionDispatchReasonCodes.DeliveryStateUnavailable);
        }
    }

    private static ProjectionDeliveryAdmissionResult Retryable(string reasonCode) => new(
        ProjectionDeliveryAdmissionDisposition.Retryable,
        reasonCode,
        null);

    private static ProjectionDeliveryAdmissionResult Failed(string reasonCode) => new(
        ProjectionDeliveryAdmissionDisposition.Failed,
        reasonCode,
        null);
}
