namespace Hexalith.EventStore.Server.Projections;

/// <summary>Validates the no-mixed-writer boundary and first-writes its store-global marker.</summary>
internal sealed class ProjectionDeliveryCutover(
    IProjectionDeliveryStateStore stateStore,
    TimeProvider timeProvider) : IProjectionDeliveryCutover {
    /// <inheritdoc/>
    public async Task<ProjectionDeliveryCutoverStatus> ActivateAsync(
        ProjectionDeliveryCutoverRequest request,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.CutoverCommit)
            || string.IsNullOrWhiteSpace(request.BackupReference)
            || !request.WritersQuiesced
            || !request.RetryWorkersQuiesced
            || !request.DowngradeProhibitedAcknowledged) {
            return ProjectionDeliveryCutoverStatus.PreconditionsFailed;
        }

        try {
            ProjectionDeliveryWriterProtocol? existing = await stateStore
                .ReadWriterProtocolAsync(cancellationToken)
                .ConfigureAwait(false);
            if (existing is not null) {
                return existing.IsCurrent
                    && string.Equals(existing.CutoverCommit, request.CutoverCommit, StringComparison.Ordinal)
                    ? ProjectionDeliveryCutoverStatus.Activated
                    : ProjectionDeliveryCutoverStatus.Conflict;
            }

            bool activated = await stateStore.TryActivateWriterProtocolAsync(
                new ProjectionDeliveryWriterProtocol(
                    ProjectionDeliveryWriterProtocol.CurrentSchemaVersion,
                    ProjectionDeliveryWriterProtocol.CurrentWriterProtocolVersion,
                    request.CutoverCommit,
                    timeProvider.GetUtcNow()),
                cancellationToken).ConfigureAwait(false);
            if (activated) {
                return ProjectionDeliveryCutoverStatus.Activated;
            }

            ProjectionDeliveryWriterProtocol? concurrent = await stateStore
                .ReadWriterProtocolAsync(cancellationToken)
                .ConfigureAwait(false);
            return concurrent?.IsCurrent == true
                && string.Equals(concurrent.CutoverCommit, request.CutoverCommit, StringComparison.Ordinal)
                    ? ProjectionDeliveryCutoverStatus.Activated
                    : ProjectionDeliveryCutoverStatus.Conflict;
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch {
            return ProjectionDeliveryCutoverStatus.StateUnavailable;
        }
    }
}
