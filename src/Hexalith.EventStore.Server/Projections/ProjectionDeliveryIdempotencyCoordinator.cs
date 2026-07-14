using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Server.Configuration;

using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>ETag-authoritative projection delivery admission and completion state machine.</summary>
internal sealed class ProjectionDeliveryIdempotencyCoordinator(
    IProjectionDeliveryStateStore stateStore,
    IOptions<ProjectionDeliveryIdempotencyOptions> options,
    TimeProvider timeProvider) : IProjectionDeliveryIdempotencyCoordinator {
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

            ProjectionDeliveryAdmissionResult? prefixConflict = ValidateCompletedPrefix(current, history);
            if (prefixConflict is not null) {
                return prefixConflict;
            }

            ProjectionDeliveryReservation? active = current.ActiveReservation;
            if (active is not null) {
                bool sameIdentity = active.EndSequence == head.SequenceNumber
                    && string.Equals(active.HeadMessageId, head.MessageId, StringComparison.Ordinal);
                if (!sameIdentity) {
                    return Retryable(ProjectionDispatchReasonCodes.DeliveryInProgress);
                }

                if (!string.Equals(active.DispatchId, head.MessageId, StringComparison.Ordinal)
                    || !string.Equals(active.ManifestFingerprint, history.PrefixFingerprint, StringComparison.Ordinal)) {
                    return Failed(ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
                }

                if (active.ExpiresAt > now) {
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
                1);
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
            if (!IsValidCurrentState(
                current,
                identity,
                projectionName,
                history.InitialFingerprint)) {
                return ProjectionDeliveryCompletion.Fenced;
            }

            if (IsExactCompleted(current, history.Events[^1])) {
                return ProjectionDeliveryCompletion.AlreadyCompleted;
            }

            if (current.ActiveReservation != reservation
                || current.LastDeliveredSequence != reservation.StartSequence - 1
                || !PrefixMatches(current, history)) {
                return ProjectionDeliveryCompletion.Fenced;
            }

            ProjectionDeliveryReceipt[] appended = [.. history.Events
                .Where(value => value.SequenceNumber >= reservation.StartSequence
                    && value.SequenceNumber <= reservation.EndSequence)
                .Select(static value => new ProjectionDeliveryReceipt(
                    value.SequenceNumber,
                    value.MessageId,
                    value.EventFingerprint,
                    value.PrefixFingerprint))];
            if (appended.Length != reservation.EndSequence - reservation.StartSequence + 1) {
                return ProjectionDeliveryCompletion.Fenced;
            }

            ProjectionDeliveryReceipt[] compacted = [.. (current.CompletedReceipts ?? [])
                .Concat(appended)
                .OrderBy(static receipt => receipt.SequenceNumber)
                .TakeLast(configured.CompletedReceiptLimit)];
            ProjectionDeliveryEventDigest completedHead = history.Events[^1];
            ProjectionDeliveryState completed = current with {
                LastDeliveredSequence = completedHead.SequenceNumber,
                LastCompletedMessageId = completedHead.MessageId,
                CompletedPrefixFingerprint = completedHead.PrefixFingerprint,
                CompletedReceipts = compacted,
                FirstRetainedSequence = compacted[0].SequenceNumber,
                ActiveReservation = null,
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
                || read.State?.ActiveReservation != reservation) {
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

    private static bool IsValidCurrentState(
        ProjectionDeliveryState state,
        AggregateIdentity identity,
        string projectionName,
        string initialFingerprint) {
        if (state.SchemaVersion != ProjectionDeliveryState.CurrentSchemaVersion
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
        foreach (ProjectionDeliveryReceipt receipt in state.CompletedReceipts) {
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

        ProjectionDeliveryReceipt? receipt = state.CompletedReceipts!
            .FirstOrDefault(value => value.SequenceNumber == head.SequenceNumber);
        if (receipt is null) {
            return Failed(ProjectionDispatchReasonCodes.DeliveryReconciliationRequired);
        }

        if (!ReceiptMatches(receipt, head)) {
            return Failed(ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
        }

        foreach (ProjectionDeliveryReceipt overlap in state.CompletedReceipts!
            .Where(value => value.SequenceNumber <= head.SequenceNumber)) {
            ProjectionDeliveryEventDigest supplied = history.Events[checked((int)overlap.SequenceNumber - 1)];
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
        if (state.LastDeliveredSequence >= history.Events.Count) {
            return Failed(ProjectionDispatchReasonCodes.DeliveryGap);
        }

        if (!PrefixMatches(state, history)) {
            return Failed(ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
        }

        foreach (ProjectionDeliveryReceipt receipt in state.CompletedReceipts!) {
            if (receipt.SequenceNumber > history.Events.Count
                || !ReceiptMatches(receipt, history.Events[checked((int)receipt.SequenceNumber - 1)])) {
                return Failed(ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
            }
        }

        return null;
    }

    private static bool PrefixMatches(
        ProjectionDeliveryState state,
        ProjectionDeliveryFingerprintHistory history) =>
        state.LastDeliveredSequence == 0
            ? string.Equals(state.CompletedPrefixFingerprint, history.InitialFingerprint, StringComparison.Ordinal)
            : state.LastDeliveredSequence <= history.Events.Count
                && string.Equals(
                    state.CompletedPrefixFingerprint,
                    history.Events[checked((int)state.LastDeliveredSequence - 1)].PrefixFingerprint,
                    StringComparison.Ordinal);

    private static bool HasMessageIdentityConflict(
        ProjectionDeliveryState state,
        ProjectionDeliveryFingerprintHistory history) {
        var supplied = history.Events.ToDictionary(static value => value.MessageId, StringComparer.Ordinal);
        return state.CompletedReceipts!.Any(receipt =>
            supplied.TryGetValue(receipt.MessageId, out ProjectionDeliveryEventDigest? value)
            && (value.SequenceNumber != receipt.SequenceNumber || !ReceiptMatches(receipt, value)));
    }

    private static bool IsExactCompleted(ProjectionDeliveryState state, ProjectionDeliveryEventDigest head) =>
        state.LastDeliveredSequence >= head.SequenceNumber
        && state.CompletedReceipts?.FirstOrDefault(value => value.SequenceNumber == head.SequenceNumber) is { } receipt
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
