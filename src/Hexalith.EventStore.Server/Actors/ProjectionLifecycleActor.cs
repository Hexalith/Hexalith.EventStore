using Dapr.Actors.Runtime;

using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>
/// DAPR actor that serializes projection delivery writes against projection erasure per
/// (tenant, domain, aggregate, projection). One actor instance per lifecycle scope makes DAPR
/// turn-based concurrency the cross-replica serialization primitive: while an erase operation
/// holds the <see cref="ProjectionLifecyclePhase.Erasing"/> phase, delivery writes are deferred.
/// </summary>
public partial class ProjectionLifecycleActor(ActorHost host, ILogger<ProjectionLifecycleActor> logger)
    : Actor(host), IProjectionLifecycleActor {
    /// <summary>
    /// The actor type name used for DAPR actor registration.
    /// </summary>
    public const string ActorTypeName = "ProjectionLifecycleActor";

    private const string LifecycleStateKey = "projection-lifecycle";

    /// <inheritdoc/>
    public async Task<ProjectionEraseAdmission> BeginEraseAsync(ProjectionEraseBeginRequest request) {
        ArgumentNullException.ThrowIfNull(request);

        ProjectionLifecycleState state = await ReadStateAsync().ConfigureAwait(false);

        if (state.Phase == ProjectionLifecyclePhase.Erasing) {
            if (string.Equals(state.OperationId, request.OperationId, StringComparison.Ordinal)) {
                Log.EraseResumed(logger, Host.Id.GetId(), request.OperationId);
                return new ProjectionEraseAdmission(
                    ProjectionEraseAdmissionKind.Resume,
                    new Dictionary<string, string>(state.PerTargetOutcomes, StringComparer.Ordinal));
            }

            Log.EraseConflict(logger, Host.Id.GetId(), request.OperationId, state.OperationId ?? string.Empty);
            return new ProjectionEraseAdmission(
                ProjectionEraseAdmissionKind.Conflict,
                new Dictionary<string, string>(StringComparer.Ordinal));
        }

        var admitted = new ProjectionLifecycleState(
            ProjectionLifecyclePhase.Erasing,
            request.OperationId,
            request.ManifestDigest,
            new Dictionary<string, string>(StringComparer.Ordinal));
        await PersistStateAsync(admitted).ConfigureAwait(false);

        Log.EraseAdmitted(logger, Host.Id.GetId(), request.OperationId, request.ManifestDigest);
        return new ProjectionEraseAdmission(
            ProjectionEraseAdmissionKind.Admitted,
            new Dictionary<string, string>(StringComparer.Ordinal));
    }

    /// <inheritdoc/>
    public async Task<bool> RecordTargetOutcomeAsync(ProjectionTargetOutcomeRequest request) {
        ArgumentNullException.ThrowIfNull(request);

        ProjectionLifecycleState state = await ReadStateAsync().ConfigureAwait(false);
        if (state.Phase != ProjectionLifecyclePhase.Erasing
            || !string.Equals(state.OperationId, request.OperationId, StringComparison.Ordinal)) {
            return false;
        }

        var outcomes = new Dictionary<string, string>(state.PerTargetOutcomes, StringComparer.Ordinal) {
            [request.TargetKey] = request.Outcome,
        };
        await PersistStateAsync(state with { PerTargetOutcomes = outcomes }).ConfigureAwait(false);

        Log.TargetOutcomeRecorded(logger, Host.Id.GetId(), request.OperationId, request.TargetKey, request.Outcome);
        return true;
    }

    /// <inheritdoc/>
    public async Task<bool> CompleteEraseAsync(ProjectionEraseCompleteRequest request) {
        ArgumentNullException.ThrowIfNull(request);

        ProjectionLifecycleState state = await ReadStateAsync().ConfigureAwait(false);
        if (state.Phase != ProjectionLifecyclePhase.Erasing
            || !string.Equals(state.OperationId, request.OperationId, StringComparison.Ordinal)) {
            return false;
        }

        await PersistStateAsync(new ProjectionLifecycleState(
                ProjectionLifecyclePhase.Idle,
                OperationId: null,
                ManifestDigest: null,
                new Dictionary<string, string>(StringComparer.Ordinal)))
            .ConfigureAwait(false);

        Log.EraseCompleted(logger, Host.Id.GetId(), request.OperationId);
        return true;
    }

    /// <inheritdoc/>
    public async Task<ProjectionDeliveryAdmission> TryAdmitDeliveryWriteAsync() {
        ProjectionLifecycleState state = await ReadStateAsync().ConfigureAwait(false);
        return new ProjectionDeliveryAdmission(state.Phase != ProjectionLifecyclePhase.Erasing, state.Phase);
    }

    private async Task<ProjectionLifecycleState> ReadStateAsync() {
        ConditionalValue<ProjectionLifecycleState> result = await StateManager
            .TryGetStateAsync<ProjectionLifecycleState>(LifecycleStateKey)
            .ConfigureAwait(false);

        return result.HasValue
            ? result.Value
            : new ProjectionLifecycleState(
                ProjectionLifecyclePhase.Idle,
                OperationId: null,
                ManifestDigest: null,
                new Dictionary<string, string>(StringComparer.Ordinal));
    }

    private async Task PersistStateAsync(ProjectionLifecycleState state) {
        await StateManager.SetStateAsync(LifecycleStateKey, state).ConfigureAwait(false);
        await StateManager.SaveStateAsync().ConfigureAwait(false);
    }

    private static partial class Log {
        [LoggerMessage(
            EventId = 5051,
            Level = LogLevel.Debug,
            Message = "Projection erase admitted. ActorId={ActorId}, OperationId={OperationId}, ManifestDigest={ManifestDigest}, Phase=Erasing")]
        public static partial void EraseAdmitted(ILogger logger, string actorId, string operationId, string manifestDigest);

        [LoggerMessage(
            EventId = 5052,
            Level = LogLevel.Debug,
            Message = "Projection erase resumed for the in-flight operation. ActorId={ActorId}, OperationId={OperationId}, Phase=Erasing")]
        public static partial void EraseResumed(ILogger logger, string actorId, string operationId);

        [LoggerMessage(
            EventId = 5053,
            Level = LogLevel.Warning,
            Message = "Projection erase refused: a different operation is in progress. ActorId={ActorId}, RequestedOperationId={RequestedOperationId}, ActiveOperationId={ActiveOperationId}, Phase=Erasing")]
        public static partial void EraseConflict(ILogger logger, string actorId, string requestedOperationId, string activeOperationId);

        [LoggerMessage(
            EventId = 5054,
            Level = LogLevel.Debug,
            Message = "Projection erase target outcome recorded. ActorId={ActorId}, OperationId={OperationId}, TargetKey={TargetKey}, Outcome={Outcome}")]
        public static partial void TargetOutcomeRecorded(ILogger logger, string actorId, string operationId, string targetKey, string outcome);

        [LoggerMessage(
            EventId = 5055,
            Level = LogLevel.Debug,
            Message = "Projection erase completed. ActorId={ActorId}, OperationId={OperationId}, Phase=Idle")]
        public static partial void EraseCompleted(ILogger logger, string actorId, string operationId);
    }
}

/// <summary>
/// Persisted lifecycle state for a single (tenant, domain, aggregate, projection) scope.
/// </summary>
/// <param name="Phase">The current lifecycle phase.</param>
/// <param name="OperationId">The in-flight erase operation identifier, or null when idle.</param>
/// <param name="ManifestDigest">The erase target manifest digest, or null when idle.</param>
/// <param name="PerTargetOutcomes">Per-target erase outcomes recorded during the operation.</param>
internal sealed record ProjectionLifecycleState(
    ProjectionLifecyclePhase Phase,
    string? OperationId,
    string? ManifestDigest,
    Dictionary<string, string> PerTargetOutcomes);
