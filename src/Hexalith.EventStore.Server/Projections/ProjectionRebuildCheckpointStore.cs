using Dapr;
using Dapr.Client;

using Hexalith.Commons.UniqueIds;
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
            // M17: persisted OperationId is read back into AdminOperationResult; reject malformed
            // ids so a tampered state-store row does not surface garbage to operators.
            if (!IsValidOperationId(checkpoint.OperationId)) {
                Log.CheckpointMalformedOperationId(logger, scope.Tenant, scope.Domain, scope.ProjectionName);
                return null;
            }
        }

        return checkpoint;
    }

    private static bool IsValidOperationId(string? operationId) {
        if (operationId is null) {
            return true;
        }

        // ULID shape per UniqueIdHelper.GenerateSortableUniqueStringId(): 26 chars Crockford base32.
        if (operationId.Length != 26) {
            return false;
        }

        foreach (char c in operationId) {
            bool isValid = (c >= '0' && c <= '9') || (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z');
            if (!isValid) {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc/>
    public async Task<ProjectionRebuildCheckpointSaveResult> SaveAsync(
        ProjectionRebuildCheckpointScope scope,
        long lastAppliedSequence,
        ProjectionRebuildStatus status,
        string? failureReasonCode = null,
        CancellationToken cancellationToken = default,
        long? toPosition = null) {
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
                    if (existing.LastAppliedSequence >= lastAppliedSequence
                        && existing.Status == status
                        && string.Equals(existing.FailureReasonCode, failureReasonCode, StringComparison.Ordinal)
                        && existing.ToPosition == toPosition) {
                        // H11: surface the caller's OperationId (if any) instead of the existing
                        // operation's, so a no-op response does not leak a prior operator's id.
                        return ProjectionRebuildCheckpointSaveResult.Success(
                            string.IsNullOrWhiteSpace(scope.OperationId)
                                ? existing
                                : existing with { OperationId = scope.OperationId });
                    }

                    // C4 + H7: SaveAsync must NOT regress lifecycle into Running from a terminal
                    // or operator-protected status. Only ResetAsync routes are permitted to flip
                    // these. Returning a typed failure lets the orchestrator stop cleanly without
                    // overwriting the operator's intent.
                    if (IsLifecycleProtected(existing.Status) && IsNonTerminalAdvancement(status)) {
                        string protectReason = existing.Status switch {
                            ProjectionRebuildStatus.Paused or ProjectionRebuildStatus.Pausing => StreamReplayReasonCodes.RebuildPaused,
                            ProjectionRebuildStatus.Canceled or ProjectionRebuildStatus.Canceling => StreamReplayReasonCodes.RebuildCanceled,
                            _ => StreamReplayReasonCodes.CheckpointConflict,
                        };
                        Log.CheckpointLifecycleProtected(logger, scope.Tenant, scope.Domain, scope.ProjectionName, existing.Status.ToString(), status.ToString(), protectReason);
                        return ProjectionRebuildCheckpointSaveResult.Failure(protectReason);
                    }
                }

                long monotonicSequence = Math.Max(existing?.LastAppliedSequence ?? 0, lastAppliedSequence);
                string? operationId = string.IsNullOrWhiteSpace(scope.OperationId)
                    ? existing?.OperationId
                    : scope.OperationId;
                var checkpoint = new ProjectionRebuildCheckpoint(
                    scope.Tenant,
                    scope.Domain,
                    scope.ProjectionName,
                    scope.AggregateId,
                    operationId,
                    monotonicSequence,
                    status,
                    DateTimeOffset.UtcNow,
                    failureReasonCode,
                    toPosition);

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
            catch (Exception ex) when (IsStateStoreUnavailable(ex)) {
                Log.CheckpointUnavailable(logger, ex, scope.Tenant, scope.Domain, scope.ProjectionName, ex.GetType().Name);
                return ProjectionRebuildCheckpointSaveResult.Failure(StreamReplayReasonCodes.CheckpointUnavailable);
            }
        }

        return ProjectionRebuildCheckpointSaveResult.Failure(StreamReplayReasonCodes.CheckpointConflict);
    }

    /// <inheritdoc/>
    public async Task<ProjectionRebuildCheckpointSaveResult> ResetAsync(
        ProjectionRebuildCheckpointScope scope,
        long lastAppliedSequence,
        ProjectionRebuildStatus status,
        string? failureReasonCode = null,
        CancellationToken cancellationToken = default,
        long? toPosition = null) {
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
                }

                var checkpoint = new ProjectionRebuildCheckpoint(
                    scope.Tenant,
                    scope.Domain,
                    scope.ProjectionName,
                    scope.AggregateId,
                    string.IsNullOrWhiteSpace(scope.OperationId) ? existing?.OperationId : scope.OperationId,
                    lastAppliedSequence,
                    status,
                    DateTimeOffset.UtcNow,
                    failureReasonCode,
                    toPosition);

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
            catch (Exception ex) when (IsStateStoreUnavailable(ex)) {
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
            string.IsNullOrWhiteSpace(scope.AggregateId) ? "*" : scope.AggregateId);
    }

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

    // M1: add '*' to reserved chars. The state-key derivation collapses null AggregateId to "*";
    // an aggregateId literally equal to "*" would have collided with the domain-wide key.
    private static readonly System.Buffers.SearchValues<char> s_reservedChars = System.Buffers.SearchValues.Create(":\0|\r\n*");

    private static bool IsLifecycleProtected(ProjectionRebuildStatus status)
        => status is ProjectionRebuildStatus.Paused
            or ProjectionRebuildStatus.Pausing
            or ProjectionRebuildStatus.Canceled
            or ProjectionRebuildStatus.Canceling
            or ProjectionRebuildStatus.Failed
            or ProjectionRebuildStatus.Succeeded;

    private static bool IsNonTerminalAdvancement(ProjectionRebuildStatus status)
        => status is ProjectionRebuildStatus.Running
            or ProjectionRebuildStatus.Resuming
            or ProjectionRebuildStatus.Retrying;

    private static void ValidateCheckpointScope(ProjectionRebuildCheckpointScope scope, ProjectionRebuildCheckpoint checkpoint) {
        if (!string.Equals(checkpoint.Tenant, scope.Tenant, StringComparison.Ordinal)
            || !string.Equals(checkpoint.Domain, scope.Domain, StringComparison.Ordinal)
            || !string.Equals(checkpoint.ProjectionName, scope.ProjectionName, StringComparison.Ordinal)
            || !string.Equals(checkpoint.AggregateId, scope.AggregateId, StringComparison.Ordinal)) {
            throw new InvalidOperationException("Projection rebuild checkpoint scope does not match the requested scope.");
        }
    }

    private static bool IsStateStoreUnavailable(Exception exception)
        => IsStateStoreUnavailable(exception, depth: 0);

    // M5: depth-bounded recursion. Maliciously-constructed or circular exception chains
    // would otherwise stack-overflow.
    private const int MaxExceptionUnwindDepth = 8;

    private static bool IsStateStoreUnavailable(Exception exception, int depth) {
        if (depth >= MaxExceptionUnwindDepth) {
            return false;
        }

        if (exception is DaprException or HttpRequestException or IOException or TimeoutException) {
            return true;
        }

        return exception.InnerException is not null
            && IsStateStoreUnavailable(exception.InnerException, depth + 1);
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

        [LoggerMessage(
            EventId = 1192,
            Level = LogLevel.Warning,
            Message = "Projection rebuild checkpoint lifecycle write rejected: TenantId={TenantId}, Domain={Domain}, ProjectionName={ProjectionName}, ExistingStatus={ExistingStatus}, AttemptedStatus={AttemptedStatus}, ReasonCode={ReasonCode}, Stage=ProjectionRebuildLifecycleProtected")]
        public static partial void CheckpointLifecycleProtected(
            ILogger logger,
            string tenantId,
            string domain,
            string projectionName,
            string existingStatus,
            string attemptedStatus,
            string reasonCode);

        [LoggerMessage(
            EventId = 1193,
            Level = LogLevel.Warning,
            Message = "Projection rebuild checkpoint malformed operation id discarded: TenantId={TenantId}, Domain={Domain}, ProjectionName={ProjectionName}, Stage=ProjectionRebuildCheckpointMalformedOperationId")]
        public static partial void CheckpointMalformedOperationId(
            ILogger logger,
            string tenantId,
            string domain,
            string projectionName);
    }
}
