using Dapr.Actors.Runtime;

using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>
/// DAPR actor that serializes projection delivery writes against rebuild and erasure per
/// (tenant, domain, aggregate, projection). One actor instance per lifecycle scope makes DAPR
/// turn-based concurrency the cross-replica serialization primitive: while rebuild or erase owns
/// a non-idle lifecycle phase, ordinary delivery writes are deferred.
/// </summary>
public partial class ProjectionLifecycleActor(
    ActorHost host,
    ILogger<ProjectionLifecycleActor> logger,
    TimeProvider? timeProvider = null)
    : Actor(host), IProjectionLifecycleActor {
    /// <summary>
    /// The actor type name used for DAPR actor registration.
    /// </summary>
    public const string ActorTypeName = "ProjectionLifecycleActor";

    private const string LifecycleStateKey = "projection-lifecycle";
    private const string LifecycleEpochStateKey = "projection-lifecycle-epoch";
    private static readonly TimeSpan DefaultDeliveryLeaseDuration = TimeSpan.FromMinutes(5);
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    /// <inheritdoc/>
    public async Task<bool> BeginRebuildAsync(ProjectionRebuildLifecycleRequest request) {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.OperationId);
        ProjectionLifecycleActorState state = await ReadStateAsync().ConfigureAwait(false);
        if (state.Phase == ProjectionLifecyclePhase.Rebuilding) {
            return string.Equals(state.OperationId, request.OperationId, StringComparison.Ordinal);
        }

        if (state.Phase != ProjectionLifecyclePhase.Idle) {
            return false;
        }

        await PersistStateAsync(new ProjectionLifecycleActorState(
                ProjectionLifecyclePhase.Rebuilding,
                request.OperationId,
                ManifestDigest: null,
                new Dictionary<string, string>(StringComparer.Ordinal),
                checked(state.Revision + 1)))
            .ConfigureAwait(false);
        return true;
    }

    /// <inheritdoc/>
    public async Task<bool> CompleteRebuildAsync(ProjectionRebuildLifecycleRequest request) {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.OperationId);
        ProjectionLifecycleActorState state = await ReadStateAsync().ConfigureAwait(false);
        if (state.Phase != ProjectionLifecyclePhase.Rebuilding
            || state.PromotionFenced
            || !string.Equals(state.OperationId, request.OperationId, StringComparison.Ordinal)) {
            return false;
        }

        await PersistStateAsync(new ProjectionLifecycleActorState(
                ProjectionLifecyclePhase.Idle,
                OperationId: null,
                ManifestDigest: null,
                new Dictionary<string, string>(StringComparer.Ordinal),
                checked(state.Revision + 1)))
            .ConfigureAwait(false);
        return true;
    }

    /// <inheritdoc/>
    public async Task<bool> BeginRebuildPromotionAsync(ProjectionRebuildLifecycleRequest request) {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.OperationId);
        ProjectionLifecycleActorState state = await ReadStateAsync().ConfigureAwait(false);
        if (state.Phase != ProjectionLifecyclePhase.Rebuilding
            || !string.Equals(state.OperationId, request.OperationId, StringComparison.Ordinal)) {
            return false;
        }

        if (state.PromotionFenced) {
            return true;
        }

        await PersistStateAsync(state with {
            PromotionFenced = true,
            Revision = checked(state.Revision + 1),
        }).ConfigureAwait(false);
        return true;
    }

    /// <inheritdoc/>
    public async Task<bool> CompleteRebuildPromotionAsync(ProjectionRebuildLifecycleRequest request) {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.OperationId);
        ProjectionLifecycleActorState state = await ReadStateAsync().ConfigureAwait(false);
        if (state.Phase != ProjectionLifecyclePhase.Rebuilding
            || !state.PromotionFenced
            || !string.Equals(state.OperationId, request.OperationId, StringComparison.Ordinal)) {
            return false;
        }

        await PersistStateAsync(state with {
            PromotionFenced = false,
            Revision = checked(state.Revision + 1),
        }).ConfigureAwait(false);
        return true;
    }

    /// <inheritdoc/>
    public async Task<ProjectionLifecyclePhase> ReadPhaseAsync()
        => (await ReadStateAsync().ConfigureAwait(false)).Phase;

    /// <inheritdoc/>
    public async Task<ProjectionLifecycleSnapshot> ReadSnapshotAsync() {
        ProjectionLifecycleActorState state = await ReadStateAsync().ConfigureAwait(false);
        return new ProjectionLifecycleSnapshot(state.Phase, state.Revision);
    }

    /// <inheritdoc/>
    public async Task<bool> BeginDeliveryWriteAsync(ProjectionDeliveryLifecycleRequest request) {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.OperationId);
        ProjectionLifecycleActorState state = await ReadStateAsync().ConfigureAwait(false);
        DateTimeOffset now = _timeProvider.GetUtcNow();
        DateTimeOffset maximumLeaseExpiry = now + DefaultDeliveryLeaseDuration;
        DateTimeOffset leaseExpiresAtUtc = request.LeaseExpiresAtUtc is { } requestedExpiry
            && requestedExpiry > now
                ? requestedExpiry < maximumLeaseExpiry ? requestedExpiry : maximumLeaseExpiry
                : maximumLeaseExpiry;
        bool reclaimExpiredDelivery = false;
        if (state.Phase == ProjectionLifecyclePhase.Delivering) {
            if (string.Equals(state.OperationId, request.OperationId, StringComparison.Ordinal)) {
                await PersistStateAsync(state with {
                    DeliveryLeaseExpiresAtUtc = leaseExpiresAtUtc,
                }).ConfigureAwait(false);
                return true;
            }

            if (state.DeliveryLeaseExpiresAtUtc is null) {
                // A pre-lease-version delivery has no safe expiry evidence. Start a bounded migration
                // lease on the first competing request; a later retry can then reclaim it safely.
                await PersistStateAsync(state with {
                    DeliveryLeaseExpiresAtUtc = maximumLeaseExpiry,
                }).ConfigureAwait(false);
                return false;
            }

            if (state.DeliveryLeaseExpiresAtUtc > now) {
                return false;
            }

            reclaimExpiredDelivery = true;
        }

        if (state.Phase != ProjectionLifecyclePhase.Idle && !reclaimExpiredDelivery) {
            return false;
        }

        await PersistStateAsync(new ProjectionLifecycleActorState(
                ProjectionLifecyclePhase.Delivering,
                request.OperationId,
                ManifestDigest: null,
                new Dictionary<string, string>(StringComparer.Ordinal),
                checked(state.Revision + 1),
                DeliveryLeaseExpiresAtUtc: leaseExpiresAtUtc))
            .ConfigureAwait(false);
        return true;
    }

    /// <inheritdoc/>
    public async Task<bool> CompleteDeliveryWriteAsync(ProjectionDeliveryLifecycleRequest request) {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.OperationId);
        ProjectionLifecycleActorState state = await ReadStateAsync().ConfigureAwait(false);
        if (state.Phase != ProjectionLifecyclePhase.Delivering
            || !string.Equals(state.OperationId, request.OperationId, StringComparison.Ordinal)) {
            return false;
        }

        // A completed normal delivery must restore the absent lifecycle baseline (absent means idle)
        // rather than leaving a persisted Idle record. Delivery is the high-frequency path: persisting an
        // Idle row for every completed aggregate delivery would accumulate lifecycle residue that erasure
        // would later have to reclaim, and would surface a spurious lifecycle row for an otherwise idle
        // projection. The transient delivery lease is cleared here while a separate monotonic epoch is
        // retained; the rebuild/erase paths keep their durable Idle transition.
        await ClearStateAsync(checked(state.Revision + 1)).ConfigureAwait(false);
        return true;
    }

    /// <inheritdoc/>
    public async Task<ProjectionEraseAdmission> BeginEraseAsync(ProjectionEraseBeginRequest request) {
        ArgumentNullException.ThrowIfNull(request);

        ProjectionLifecycleActorState state = await ReadStateAsync().ConfigureAwait(false);

        if (state.Phase is ProjectionLifecyclePhase.Rebuilding or ProjectionLifecyclePhase.Delivering) {
            Log.EraseConflict(logger, Host.Id.GetId(), request.OperationId, state.OperationId ?? string.Empty);
            return new ProjectionEraseAdmission(
                ProjectionEraseAdmissionKind.Conflict,
                new Dictionary<string, string>(StringComparer.Ordinal));
        }

        if (state.Phase == ProjectionLifecyclePhase.Erasing) {
            if (string.Equals(state.OperationId, request.OperationId, StringComparison.Ordinal)) {
                if (!string.Equals(state.ManifestDigest, request.ManifestDigest, StringComparison.Ordinal)) {
                    // Same operationId but a different target manifest: refuse rather than resume against a
                    // manifest that was never admitted. Resume must reuse the recorded manifest/progress.
                    Log.EraseManifestMismatch(logger, Host.Id.GetId(), request.OperationId);
                    return new ProjectionEraseAdmission(
                        ProjectionEraseAdmissionKind.Conflict,
                        new Dictionary<string, string>(StringComparer.Ordinal));
                }

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

        if (!request.AllowBegin) {
            Log.FreshBeginNotAllowed(logger, Host.Id.GetId(), request.OperationId);
            return new ProjectionEraseAdmission(
                ProjectionEraseAdmissionKind.BeginNotAllowed,
                new Dictionary<string, string>(StringComparer.Ordinal));
        }

        var admitted = new ProjectionLifecycleActorState(
            ProjectionLifecyclePhase.Erasing,
            request.OperationId,
            request.ManifestDigest,
            new Dictionary<string, string>(StringComparer.Ordinal),
            checked(state.Revision + 1));
        await PersistStateAsync(admitted).ConfigureAwait(false);

        Log.EraseAdmitted(logger, Host.Id.GetId(), request.OperationId, request.ManifestDigest);
        return new ProjectionEraseAdmission(
            ProjectionEraseAdmissionKind.Admitted,
            new Dictionary<string, string>(StringComparer.Ordinal));
    }

    /// <inheritdoc/>
    public async Task<bool> RecordTargetOutcomeAsync(ProjectionTargetOutcomeRequest request) {
        ArgumentNullException.ThrowIfNull(request);

        ProjectionLifecycleActorState state = await ReadStateAsync().ConfigureAwait(false);
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

        ProjectionLifecycleActorState state = await ReadStateAsync().ConfigureAwait(false);
        if (state.Phase != ProjectionLifecyclePhase.Erasing
            || !string.Equals(state.OperationId, request.OperationId, StringComparison.Ordinal)) {
            return false;
        }

        await PersistStateAsync(new ProjectionLifecycleActorState(
                ProjectionLifecyclePhase.Idle,
                OperationId: null,
                ManifestDigest: null,
                new Dictionary<string, string>(StringComparer.Ordinal),
                checked(state.Revision + 1)))
            .ConfigureAwait(false);

        Log.EraseCompleted(logger, Host.Id.GetId(), request.OperationId);
        return true;
    }

    /// <inheritdoc/>
    public async Task<ProjectionDeliveryAdmission> TryAdmitDeliveryWriteAsync() {
        ProjectionLifecycleActorState state = await ReadStateAsync().ConfigureAwait(false);
        return new ProjectionDeliveryAdmission(state.Phase == ProjectionLifecyclePhase.Idle, state.Phase);
    }

    private async Task<ProjectionLifecycleActorState> ReadStateAsync() {
        ConditionalValue<ProjectionLifecycleActorState> result = await StateManager
            .TryGetStateAsync<ProjectionLifecycleActorState>(LifecycleStateKey)
            .ConfigureAwait(false);

        if (result.HasValue) {
            return result.Value;
        }

        ConditionalValue<long> epoch = await StateManager
            .TryGetStateAsync<long>(LifecycleEpochStateKey)
            .ConfigureAwait(false);
        return new ProjectionLifecycleActorState(
            ProjectionLifecyclePhase.Idle,
            OperationId: null,
            ManifestDigest: null,
            new Dictionary<string, string>(StringComparer.Ordinal),
            epoch.HasValue ? epoch.Value : 0);
    }

    private async Task PersistStateAsync(ProjectionLifecycleActorState state) {
        await StateManager.SetStateAsync(LifecycleStateKey, state).ConfigureAwait(false);
        await StateManager.SetStateAsync(LifecycleEpochStateKey, state.Revision).ConfigureAwait(false);
        await StateManager.SaveStateAsync().ConfigureAwait(false);
    }

    private async Task ClearStateAsync(long nextEpoch) {
        // Commit the post-delivery epoch and transient lease removal together. The durable epoch
        // prevents an Idle(n) -> Delivering(n+1) -> Idle(n) ABA observation without retaining the
        // high-frequency lifecycle row that the absent-idle cleanup contract forbids.
        await StateManager.SetStateAsync(LifecycleEpochStateKey, nextEpoch).ConfigureAwait(false);
        await StateManager.RemoveStateAsync(LifecycleStateKey).ConfigureAwait(false);
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
            EventId = 5056,
            Level = LogLevel.Warning,
            Message = "Projection erase refused: the same operation resumed with a different target manifest. ActorId={ActorId}, OperationId={OperationId}, Phase=Erasing")]
        public static partial void EraseManifestMismatch(ILogger logger, string actorId, string operationId);

        [LoggerMessage(
            EventId = 5055,
            Level = LogLevel.Debug,
            Message = "Projection erase completed. ActorId={ActorId}, OperationId={OperationId}, Phase=Idle")]
        public static partial void EraseCompleted(ILogger logger, string actorId, string operationId);

        [LoggerMessage(
            EventId = 5057,
            Level = LogLevel.Debug,
            Message = "Projection erase fresh begin refused by caller gate. ActorId={ActorId}, OperationId={OperationId}, Phase=Idle")]
        public static partial void FreshBeginNotAllowed(ILogger logger, string actorId, string operationId);
    }
}
