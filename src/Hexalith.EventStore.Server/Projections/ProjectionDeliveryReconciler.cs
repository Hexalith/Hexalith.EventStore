using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Server.Configuration;

using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>Hydrates delivery identity evidence from the authoritative EventStore prefix.</summary>
internal sealed class ProjectionDeliveryReconciler(
    IProjectionDeliveryStateStore stateStore,
    IProjectionDeliveryHistoryReader historyReader,
    IOptions<ProjectionDeliveryIdempotencyOptions> options,
    TimeProvider timeProvider) : IProjectionDeliveryReconciler {
    /// <inheritdoc/>
    public async Task<ProjectionDeliveryReconciliationResult> ReconcileFromEventStoreAsync(
        AggregateIdentity identity,
        string projectionName,
        string operatorId,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(identity);
        ProjectionKeySegments.Validate(projectionName, nameof(projectionName));
        ArgumentException.ThrowIfNullOrWhiteSpace(operatorId);
        ProjectionDeliveryIdempotencyOptions configured = options.Value;
        configured.Validate();
        long lastObservedSequence = 0;

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
                return new ProjectionDeliveryReconciliationResult(
                    ProjectionDeliveryReconciliationStatus.StateUnavailable,
                    ProjectionDispatchReasonCodes.DeliveryStateUnavailable,
                    0);
            }

            ProjectionDeliveryState? observed = read.State;
            if (observed is not null
                && (!string.Equals(observed.TenantId, identity.TenantId, StringComparison.Ordinal)
                    || !string.Equals(observed.Domain, identity.Domain, StringComparison.Ordinal)
                    || !string.Equals(observed.AggregateId, identity.AggregateId, StringComparison.Ordinal)
                    || (observed.ProjectionName is not null
                        && !string.Equals(observed.ProjectionName, projectionName, StringComparison.Ordinal)))) {
                return new ProjectionDeliveryReconciliationResult(
                    ProjectionDeliveryReconciliationStatus.ScopeDenied,
                    ProjectionDispatchReasonCodes.DeliveryReconciliationRequired,
                    observed.LastDeliveredSequence);
            }

            long preservedSequence = observed?.LastDeliveredSequence ?? 0;
            lastObservedSequence = preservedSequence;
            if (read.Classification is ProjectionDeliveryStateClassification.SchemaRegression
                or ProjectionDeliveryStateClassification.Unsupported) {
                return await RecordRebuildRequiredAsync(
                    identity,
                    projectionName,
                    operatorId,
                    preservedSequence,
                    observed?.SchemaVersion ?? 0,
                    cancellationToken).ConfigureAwait(false);
            }

            if (observed?.ActiveReservation is not null) {
                return new ProjectionDeliveryReconciliationResult(
                    ProjectionDeliveryReconciliationStatus.StateUnavailable,
                    ProjectionDispatchReasonCodes.DeliveryInProgress,
                    preservedSequence);
            }

            IReadOnlyList<ProjectionEventDto> events;
            ProjectionDeliveryFingerprintHistory? history = null;
            try {
                events = await historyReader.ReadAsync(identity, preservedSequence, cancellationToken).ConfigureAwait(false);
                if (preservedSequence > 0) {
                    history = ProjectionDeliveryFingerprint.ComputeHistory(identity, projectionName, events);
                    if (history.Events.Count != preservedSequence
                        || history.Events[^1].SequenceNumber != preservedSequence) {
                        return await RecordRebuildRequiredAsync(
                            identity,
                            projectionName,
                            operatorId,
                            preservedSequence,
                            observed?.SchemaVersion ?? 0,
                            cancellationToken).ConfigureAwait(false);
                    }

                    if (read.Classification == ProjectionDeliveryStateClassification.Current
                        && observed is not null
                        && (!string.Equals(
                                observed.CompletedPrefixFingerprint,
                                history.PrefixFingerprint,
                                StringComparison.Ordinal)
                            || !string.Equals(
                                observed.LastCompletedMessageId,
                                history.Events[^1].MessageId,
                                StringComparison.Ordinal))) {
                        return await RecordRebuildRequiredAsync(
                            identity,
                            projectionName,
                            operatorId,
                            preservedSequence,
                            observed.SchemaVersion,
                            cancellationToken).ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException) {
                throw;
            }
            catch (Exception exception) when (exception is ProjectionDeliveryHistoryValidationException or ArgumentException) {
                return await RecordRebuildRequiredAsync(
                    identity,
                    projectionName,
                    operatorId,
                    preservedSequence,
                    observed?.SchemaVersion ?? 0,
                    cancellationToken).ConfigureAwait(false);
            }
            catch {
                return new ProjectionDeliveryReconciliationResult(
                    ProjectionDeliveryReconciliationStatus.StateUnavailable,
                    ProjectionDispatchReasonCodes.DeliveryStateUnavailable,
                    preservedSequence);
            }

            string initial = ProjectionDeliveryFingerprint.ComputeInitial(identity, projectionName);
            ProjectionDeliveryReceipt[] receipts = history is null
                ? []
                : [.. history.Events
                    .TakeLast(configured.CompletedReceiptLimit)
                    .Select(static value => new ProjectionDeliveryReceipt(
                        value.SequenceNumber,
                        value.MessageId,
                        value.EventFingerprint,
                        value.PrefixFingerprint))];
            DateTimeOffset reconciledAt = timeProvider.GetUtcNow();
            ProjectionDeliveryState hydrated = new(
                ProjectionDeliveryState.CurrentSchemaVersion,
                ProjectionDeliveryState.CurrentWriterProtocolVersion,
                identity.TenantId,
                identity.Domain,
                identity.AggregateId,
                projectionName,
                preservedSequence,
                history?.Events[^1].MessageId,
                history?.PrefixFingerprint ?? initial,
                receipts,
                receipts.Length == 0 ? 0 : receipts[0].SequenceNumber,
                null,
                preservedSequence == 0
                    ? ProjectionDeliveryMigrationProvenance.InitializedFromZero
                    : ProjectionDeliveryMigrationProvenance.HydratedFromPersistedCheckpoint,
                reconciledAt);
            var work = new ProjectionDeliveryReconciliationWork(
                identity.TenantId,
                identity.Domain,
                identity.AggregateId,
                projectionName,
                ProjectionDispatchReasonCodes.DeliveryReconciled,
                preservedSequence,
                hydrated.SchemaVersion,
                operatorId,
                reconciledAt);
            bool saved;
            try {
                saved = await stateStore
                    .TrySaveWithReconciliationAsync(
                        identity,
                        projectionName,
                        hydrated,
                        work,
                        read.Etag,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) {
                throw;
            }
            catch {
                saved = false;
            }

            if (!saved) {
                continue;
            }

            return new ProjectionDeliveryReconciliationResult(
                ProjectionDeliveryReconciliationStatus.Completed,
                ProjectionDispatchReasonCodes.DeliveryReconciled,
                preservedSequence);
        }

        return new ProjectionDeliveryReconciliationResult(
            ProjectionDeliveryReconciliationStatus.StateUnavailable,
            ProjectionDispatchReasonCodes.DeliveryStateUnavailable,
            lastObservedSequence);
    }

    private async Task<ProjectionDeliveryReconciliationResult> RecordRebuildRequiredAsync(
        AggregateIdentity identity,
        string projectionName,
        string operatorId,
        long sequence,
        int stateVersion,
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
                    ProjectionDispatchReasonCodes.DeliveryRebuildRequired,
                    sequence,
                    stateVersion,
                    operatorId,
                    timeProvider.GetUtcNow()),
                cancellationToken).ConfigureAwait(false);
            return new ProjectionDeliveryReconciliationResult(
                ProjectionDeliveryReconciliationStatus.RebuildRequired,
                ProjectionDispatchReasonCodes.DeliveryRebuildRequired,
                sequence);
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch {
            return new ProjectionDeliveryReconciliationResult(
                ProjectionDeliveryReconciliationStatus.StateUnavailable,
                ProjectionDispatchReasonCodes.DeliveryStateUnavailable,
                sequence);
        }
    }
}
