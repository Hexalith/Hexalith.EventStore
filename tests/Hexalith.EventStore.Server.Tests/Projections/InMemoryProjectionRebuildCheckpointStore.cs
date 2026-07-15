using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.Server.Projections;

namespace Hexalith.EventStore.Server.Tests.Projections;

internal sealed class InMemoryProjectionRebuildCheckpointStore : IProjectionRebuildCheckpointStore {
    private readonly Dictionary<string, ProjectionRebuildCheckpoint> _checkpoints = new(StringComparer.Ordinal);

    public void Seed(ProjectionRebuildCheckpoint checkpoint) {
        ArgumentNullException.ThrowIfNull(checkpoint);
        _checkpoints[Key(new ProjectionRebuildCheckpointScope(
            checkpoint.Tenant,
            checkpoint.Domain,
            checkpoint.ProjectionName,
            checkpoint.AggregateId,
            checkpoint.OperationId))] = checkpoint;
    }

    public ProjectionRebuildCheckpoint? Snapshot(ProjectionRebuildCheckpointScope scope)
        => _checkpoints.GetValueOrDefault(Key(scope));

    public Task<ProjectionRebuildCheckpoint?> ReadAsync(
        ProjectionRebuildCheckpointScope scope,
        CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Snapshot(scope));
    }

    public Task<ProjectionRebuildCheckpointSaveResult> SaveAsync(
        ProjectionRebuildCheckpointScope scope,
        long lastAppliedSequence,
        ProjectionRebuildStatus status,
        string? failureReasonCode = null,
        CancellationToken cancellationToken = default,
        long? toPosition = null,
        bool isPerAggregateProgress = false) {
        cancellationToken.ThrowIfCancellationRequested();
        ProjectionRebuildCheckpoint checkpoint = Create(scope, lastAppliedSequence, status, failureReasonCode, toPosition);
        _checkpoints[Key(scope)] = checkpoint;
        return Task.FromResult(ProjectionRebuildCheckpointSaveResult.Success(checkpoint));
    }

    public Task<ProjectionRebuildCheckpointSaveResult> ResetAsync(
        ProjectionRebuildCheckpointScope scope,
        long lastAppliedSequence,
        ProjectionRebuildStatus status,
        string? failureReasonCode = null,
        CancellationToken cancellationToken = default,
        long? toPosition = null) {
        cancellationToken.ThrowIfCancellationRequested();
        ProjectionRebuildCheckpoint checkpoint = Create(scope, lastAppliedSequence, status, failureReasonCode, toPosition);
        _checkpoints[Key(scope)] = checkpoint;
        return Task.FromResult(ProjectionRebuildCheckpointSaveResult.Success(checkpoint));
    }

    public Task<bool> HasActiveOperatorRebuildForDomainAsync(
        string tenant,
        string domain,
        CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_checkpoints.Values.Any(checkpoint =>
            string.Equals(checkpoint.Tenant, tenant, StringComparison.Ordinal)
            && string.Equals(checkpoint.Domain, domain, StringComparison.Ordinal)
            && checkpoint.AggregateId is null
            && checkpoint.Status is ProjectionRebuildStatus.Running
                or ProjectionRebuildStatus.Resuming
                or ProjectionRebuildStatus.Retrying));
    }

    public Task<IReadOnlyCollection<(string Tenant, string Domain)>> ListActiveRebuildIndexPairsAsync(
        CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyCollection<(string Tenant, string Domain)> pairs = [.. _checkpoints.Values
            .Where(static checkpoint => checkpoint.AggregateId is null
                && checkpoint.Status is ProjectionRebuildStatus.Running
                    or ProjectionRebuildStatus.Resuming
                    or ProjectionRebuildStatus.Retrying)
            .Select(static checkpoint => (checkpoint.Tenant, checkpoint.Domain))
            .Distinct()];
        return Task.FromResult(pairs);
    }

    public Task<int> ClearOrphanActiveRebuildIndexEntriesAsync(
        string tenant,
        string domain,
        CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(0);
    }

    private ProjectionRebuildCheckpoint Create(
        ProjectionRebuildCheckpointScope scope,
        long lastAppliedSequence,
        ProjectionRebuildStatus status,
        string? failureReasonCode,
        long? toPosition) {
        ProjectionRebuildCheckpoint? existing = Snapshot(scope);
        return new ProjectionRebuildCheckpoint(
            scope.Tenant,
            scope.Domain,
            scope.ProjectionName,
            scope.AggregateId,
            scope.OperationId ?? existing?.OperationId,
            lastAppliedSequence,
            status,
            DateTimeOffset.UtcNow,
            failureReasonCode,
            toPosition ?? existing?.ToPosition);
    }

    private static string Key(ProjectionRebuildCheckpointScope scope)
        => $"{scope.Tenant}\u001f{scope.Domain}\u001f{scope.ProjectionName}\u001f{scope.AggregateId}";
}
