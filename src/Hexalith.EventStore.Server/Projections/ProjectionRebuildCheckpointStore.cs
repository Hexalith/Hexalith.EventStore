using Dapr.Client;

using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.Server.Configuration;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>
/// DAPR state-store implementation for projection rebuild checkpoint progress.
/// </summary>
public sealed partial class ProjectionRebuildCheckpointStore(
    DaprClient daprClient,
    IOptions<ProjectionOptions> options,
    ILogger<ProjectionRebuildCheckpointStore> logger) : IProjectionRebuildCheckpointStore {
    private const int MaxEtagRetries = 3;
    private const string StateKeyPrefix = "projection-rebuild-checkpoints:";

    /// <inheritdoc/>
    public async Task<ProjectionRebuildCheckpoint?> ReadAsync(
        ProjectionRebuildCheckpointScope scope,
        CancellationToken cancellationToken = default) {
        ValidateScope(scope);
        ProjectionRebuildCheckpoint? checkpoint = await daprClient
            .GetStateAsync<ProjectionRebuildCheckpoint>(
                options.Value.CheckpointStateStoreName,
                GetStateKey(scope),
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        if (checkpoint is not null) {
            ValidateCheckpointScope(scope, checkpoint);
        }

        return checkpoint;
    }

    /// <inheritdoc/>
    public async Task<ProjectionRebuildCheckpointSaveResult> SaveAsync(
        ProjectionRebuildCheckpointScope scope,
        long lastAppliedSequence,
        ProjectionRebuildStatus status,
        string? failureReasonCode = null,
        CancellationToken cancellationToken = default) {
        ValidateScope(scope);
        ArgumentOutOfRangeException.ThrowIfNegative(lastAppliedSequence);

        string key = GetStateKey(scope);
        string stateStoreName = options.Value.CheckpointStateStoreName;
        for (int attempt = 0; attempt < MaxEtagRetries; attempt++) {
            try {
                (ProjectionRebuildCheckpoint? existing, string etag) = await daprClient
                    .GetStateAndETagAsync<ProjectionRebuildCheckpoint>(
                        stateStoreName,
                        key,
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

                if (existing is not null) {
                    ValidateCheckpointScope(scope, existing);
                    // P2/P14: Idempotent no-op ONLY when every observable field matches.
                    // The previous "progress-status short-circuit" silently dropped real
                    // lifecycle transitions (e.g., Paused -> Running with lastAppliedSequence=0
                    // returned Success without writing, leaving the status as Paused).
                    if (existing.LastAppliedSequence >= lastAppliedSequence
                        && existing.Status == status
                        && string.Equals(existing.FailureReasonCode, failureReasonCode, StringComparison.Ordinal)) {
                        return ProjectionRebuildCheckpointSaveResult.Success(existing);
                    }
                }

                long monotonicSequence = Math.Max(existing?.LastAppliedSequence ?? 0, lastAppliedSequence);
                var checkpoint = new ProjectionRebuildCheckpoint(
                    scope.Tenant,
                    scope.Domain,
                    scope.ProjectionName,
                    scope.AggregateId,
                    scope.OperationId,
                    monotonicSequence,
                    status,
                    DateTimeOffset.UtcNow,
                    failureReasonCode);

                bool saved = await daprClient
                    .TrySaveStateAsync(
                        stateStoreName,
                        key,
                        checkpoint,
                        etag,
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                if (saved) {
                    return ProjectionRebuildCheckpointSaveResult.Success(checkpoint);
                }

                Log.CheckpointConflict(logger, scope.Tenant, scope.Domain, scope.ProjectionName, scope.AggregateId ?? string.Empty, scope.OperationId ?? string.Empty, attempt + 1);
            }
            catch (OperationCanceledException) {
                throw;
            }
            catch (Exception ex) {
                Log.CheckpointUnavailable(logger, ex, scope.Tenant, scope.Domain, scope.ProjectionName, ex.GetType().Name);
                return ProjectionRebuildCheckpointSaveResult.Failure(StreamReplayReasonCodes.CheckpointUnavailable);
            }
        }

        return ProjectionRebuildCheckpointSaveResult.Failure(StreamReplayReasonCodes.CheckpointConflict);
    }

    internal static string GetStateKey(ProjectionRebuildCheckpointScope scope) {
        ArgumentNullException.ThrowIfNull(scope);
        return string.Concat(
            StateKeyPrefix,
            scope.Tenant,
            ":",
            scope.Domain,
            ":",
            scope.ProjectionName,
            ":",
            string.IsNullOrWhiteSpace(scope.AggregateId) ? "*" : scope.AggregateId,
            ":",
            string.IsNullOrWhiteSpace(scope.OperationId) ? "*" : scope.OperationId);
    }

    private static bool IsProgressStatus(ProjectionRebuildStatus status)
        => status is ProjectionRebuildStatus.Running or ProjectionRebuildStatus.Pausing or ProjectionRebuildStatus.Resuming or ProjectionRebuildStatus.Retrying;

    private static void ValidateScope(ProjectionRebuildCheckpointScope scope) {
        ArgumentNullException.ThrowIfNull(scope);
        ValidateKeyPart(scope.Tenant, nameof(scope.Tenant));
        ValidateKeyPart(scope.Domain, nameof(scope.Domain));
        ValidateKeyPart(scope.ProjectionName, nameof(scope.ProjectionName));
        if (!string.IsNullOrWhiteSpace(scope.AggregateId)) {
            ValidateKeyPart(scope.AggregateId, nameof(scope.AggregateId));
        }

        if (!string.IsNullOrWhiteSpace(scope.OperationId)) {
            ValidateKeyPart(scope.OperationId, nameof(scope.OperationId));
        }
    }

    private static void ValidateKeyPart(string value, string parameterName) {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.AsSpan().IndexOfAny(s_reservedChars) >= 0) {
            throw new ArgumentException(
                $"{parameterName} must not contain ':', '\\0', '|', '\\r', or '\\n'.",
                parameterName);
        }
    }

    private static readonly System.Buffers.SearchValues<char> s_reservedChars = System.Buffers.SearchValues.Create(":\0|\r\n");

    private static void ValidateCheckpointScope(ProjectionRebuildCheckpointScope scope, ProjectionRebuildCheckpoint checkpoint) {
        if (!string.Equals(checkpoint.Tenant, scope.Tenant, StringComparison.Ordinal)
            || !string.Equals(checkpoint.Domain, scope.Domain, StringComparison.Ordinal)
            || !string.Equals(checkpoint.ProjectionName, scope.ProjectionName, StringComparison.Ordinal)
            || !string.Equals(checkpoint.AggregateId, scope.AggregateId, StringComparison.Ordinal)
            || !string.Equals(checkpoint.OperationId, scope.OperationId, StringComparison.Ordinal)) {
            throw new InvalidOperationException("Projection rebuild checkpoint scope does not match the requested scope.");
        }
    }

    private static partial class Log {
        [LoggerMessage(
            EventId = 1190,
            Level = LogLevel.Debug,
            Message = "Projection rebuild checkpoint ETag conflict: TenantId={TenantId}, Domain={Domain}, ProjectionName={ProjectionName}, AggregateId={AggregateId}, OperationId={OperationId}, Attempt={Attempt}, Stage=ProjectionRebuildCheckpointConflict")]
        public static partial void CheckpointConflict(
            ILogger logger,
            string tenantId,
            string domain,
            string projectionName,
            string aggregateId,
            string operationId,
            int attempt);

        [LoggerMessage(
            EventId = 1191,
            Level = LogLevel.Warning,
            Message = "Projection rebuild checkpoint store unavailable: TenantId={TenantId}, Domain={Domain}, ProjectionName={ProjectionName}, ExceptionType={ExceptionType}, Stage=ProjectionRebuildCheckpointUnavailable")]
        public static partial void CheckpointUnavailable(
            ILogger logger,
            Exception exception,
            string tenantId,
            string domain,
            string projectionName,
            string exceptionType);
    }
}
