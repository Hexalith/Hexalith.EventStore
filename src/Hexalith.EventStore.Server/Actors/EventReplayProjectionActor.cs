
using Dapr.Actors.Runtime;
using Dapr.Actors.Client;
using System.Security.Cryptography;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Server.Queries;
using Hexalith.EventStore.Server.Projections;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>
/// Concrete projection actor that stores projection state received from domain services
/// and serves it on query cache-miss via <see cref="CachingProjectionActor"/>.
/// </summary>
/// <remarks>
/// <para>
/// Registered with DAPR actor type name <c>"ProjectionActor"</c> to match
/// <see cref="QueryRouter.ProjectionActorTypeName"/>.
/// </para>
/// <para>
/// Caching interaction:
/// 1. Orchestrator calls <see cref="UpdateProjectionAsync"/> → state written to DAPR state → ETag regenerated.
/// 2. Query arrives → <see cref="CachingProjectionActor.QueryAsync"/> checks in-memory ETag cache.
/// 3. Cache miss (ETag changed) → calls <see cref="ExecuteQueryAsync"/> → reads from DAPR actor state → base caches in memory.
/// 4. Cache hit (same ETag) → returns in-memory cached payload directly (no DAPR state read).
/// </para>
/// </remarks>
public partial class EventReplayProjectionActor(
    ActorHost host,
    IETagService eTagService,
    IProjectionChangeNotifier projectionChangeNotifier,
    ILogger<EventReplayProjectionActor> logger)
    : CachingProjectionActor(host, eTagService, logger), IProjectionWriteActor, IProjectionRebuildWriteActor, IProjectionRebuildInspectionActor, IProjectionReplayPublicationActor {
    private IProjectionLifecycleGateway? _publicationLifecycle;

    /// <summary>Creates a projection actor with actor-owned lifecycle-fenced replay publication.</summary>
    [ActivatorUtilitiesConstructor]
    public EventReplayProjectionActor(ActorHost host, IETagService eTagService,
        IProjectionChangeNotifier projectionChangeNotifier, ILogger<EventReplayProjectionActor> logger,
        IActorProxyFactory actorProxyFactory) : this(host, eTagService, projectionChangeNotifier, logger)
    {
        _publicationLifecycle = new DaprProjectionLifecycleGateway(actorProxyFactory);
    }

    /// <summary>Sets the owning lifecycle seam for production protocol fixtures.</summary>
    internal void SetPublicationLifecycle(IProjectionLifecycleGateway lifecycle) => _publicationLifecycle = lifecycle;
    internal const string ProjectionStateKey = "projection-state";
    internal const string ProjectionRebuildCandidateKey = "projection-rebuild-candidate";
    internal const string ProjectionRebuildPromotionReceiptKey = "projection-rebuild-promotion";

    /// <inheritdoc/>
    public Task UpdateProjectionAsync(ProjectionState state) =>
        UpdateProjectionAsync(state, CancellationToken.None);

    /// <summary>
    /// Persists projection state and notifies subscribers with cancellation support for EventStore-owned state operations.
    /// </summary>
    /// <param name="state">The projection state to persist.</param>
    /// <param name="cancellationToken">The token to pass to DAPR actor state and notification operations.</param>
    /// <returns>A task that represents the asynchronous update operation.</returns>
    public async Task UpdateProjectionAsync(ProjectionState state, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(state.ProjectionType);
        ArgumentException.ThrowIfNullOrWhiteSpace(state.TenantId);
        state = state.WithBoundedNotification();

        // 1. Persist to DAPR actor state
        await SetPublicationStateAsync(ProjectionStateKey, state, cancellationToken).ConfigureAwait(false);
        await SavePublicationStateAsync(cancellationToken).ConfigureAwait(false);

        Log.ProjectionStatePersisted(logger, Id.GetId(), state.ProjectionType, state.TenantId);
        cancellationToken.ThrowIfCancellationRequested();

        // 2. Regenerate ETag + broadcast SignalR (fail-open on notifier path)
        try {
            await NotifyProjectionHintsAsync(state, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) {
            // Bare OperationCanceledException is rethrown so cancellation is not converted into a
            // notifier-fail-open log entry (AC9). Notifier paths that wrap OCE inside another
            // exception (e.g. SignalR transport / DAPR pub-sub wrappers) fall through to the generic
            // catch below and the documented fail-open path. See Dapr.Actors / SignalR transport
            // docs on exception propagation; revisit if production telemetry shows wrapping here.
            throw;
        }
        catch (Exception ex) {
            Log.ProjectionChangeNotificationFailed(logger, Id.GetId(), state.ProjectionType, state.TenantId, ex.GetType().Name);
        }
    }

    /// <inheritdoc/>
    public async Task StageProjectionAsync(ProjectionRebuildCandidate request) {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.OperationId);
        ArgumentNullException.ThrowIfNull(request.State);
        request = request with { State = request.State.WithBoundedNotification() };
        ValidateState(request.State);
        await ClearPublicationCacheAsync().ConfigureAwait(false);

        ConditionalValue<ProjectionRebuildPromotionReceipt> receipt = await ReadPublicationStateAsync<ProjectionRebuildPromotionReceipt>(ProjectionRebuildPromotionReceiptKey)
            .ConfigureAwait(false);
        if (receipt.HasValue
            && string.Equals(receipt.Value.OperationId, request.OperationId, StringComparison.Ordinal)) {
            return;
        }

        if (receipt.HasValue) {
            await RemovePublicationStateAsync(ProjectionRebuildPromotionReceiptKey).ConfigureAwait(false);
        }

        ConditionalValue<ProjectionRebuildCandidate> previousCandidate = await ReadPublicationStateAsync<ProjectionRebuildCandidate>(ProjectionRebuildCandidateKey).ConfigureAwait(false);
        await SetPublicationStateAsync(ProjectionRebuildCandidateKey, request with
        { PublicationFloor = receipt.HasValue ? receipt.Value.PublicationFloor ?? receipt.Value.PublicationBinding
            : previousCandidate.HasValue ? previousCandidate.Value.PublicationFloor : request.PublicationFloor,
            PriorReplayFence = receipt.HasValue ? receipt.Value.SelectedReplayFence
                : previousCandidate.HasValue ? previousCandidate.Value.PriorReplayFence : request.PriorReplayFence })
            .ConfigureAwait(false);
        await SaveRebuildStateAsync().ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<bool> PromoteProjectionAsync(ProjectionRebuildCandidateOperation request) {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.OperationId);
        await ClearPublicationCacheAsync().ConfigureAwait(false);
        ConditionalValue<ProjectionRebuildPromotionReceipt> existingReceipt = await ReadPublicationStateAsync<ProjectionRebuildPromotionReceipt>(ProjectionRebuildPromotionReceiptKey)
            .ConfigureAwait(false);
        if (existingReceipt.HasValue
            && string.Equals(existingReceipt.Value.OperationId, request.OperationId, StringComparison.Ordinal)) {
            ProjectionRebuildInspection selected = await InspectProjectionRebuildAsync(request).ConfigureAwait(false);
            return selected.Status == ProjectionRebuildInspectionStatus.Promoted;
        }

        ConditionalValue<ProjectionRebuildCandidate> candidate = await ReadPublicationStateAsync<ProjectionRebuildCandidate>(ProjectionRebuildCandidateKey)
            .ConfigureAwait(false);
        if (!candidate.HasValue
            || !string.Equals(candidate.Value.OperationId, request.OperationId, StringComparison.Ordinal)) {
            return false;
        }

        ValidateState(candidate.Value.State);
        ConditionalValue<ProjectionState> previous = await ReadPublicationStateAsync<ProjectionState>(ProjectionStateKey)
            .ConfigureAwait(false);
        await SetPublicationStateAsync(ProjectionStateKey, candidate.Value.State).ConfigureAwait(false);
        await SetPublicationStateAsync(
            ProjectionRebuildPromotionReceiptKey,
            new ProjectionRebuildPromotionReceipt(
                request.OperationId,
                previous.HasValue ? previous.Value : null,
                candidate.Value.State) { PublicationFloor = candidate.Value.PublicationFloor,
                    SelectedReplayFence = candidate.Value.SelectedReplayFence ?? candidate.Value.PriorReplayFence,
                    PriorReplayFence = candidate.Value.PriorReplayFence,
                    SelectedStateFingerprint = Convert.ToHexStringLower(SHA256.HashData(candidate.Value.State.StateBytes)) }).ConfigureAwait(false);
        await RemovePublicationStateAsync(ProjectionRebuildCandidateKey).ConfigureAwait(false);
        await SaveRebuildStateAsync().ConfigureAwait(false);

        Log.ProjectionStatePersisted(
            logger,
            Id.GetId(),
            candidate.Value.State.ProjectionType,
            candidate.Value.State.TenantId);
        try {
            await NotifyProjectionHintsAsync(candidate.Value.State).ConfigureAwait(false);
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (Exception ex) {
            Log.ProjectionChangeNotificationFailed(
                logger,
                Id.GetId(),
                candidate.Value.State.ProjectionType,
                candidate.Value.State.TenantId,
                ex.GetType().Name);
        }

        return true;
    }

    /// <inheritdoc/>
    public async Task<bool> DiscardProjectionAsync(ProjectionRebuildCandidateOperation request) {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.OperationId);
        await ClearPublicationCacheAsync().ConfigureAwait(false);
        ConditionalValue<ProjectionRebuildCandidate> candidate = await ReadPublicationStateAsync<ProjectionRebuildCandidate>(ProjectionRebuildCandidateKey)
            .ConfigureAwait(false);
        if (!candidate.HasValue) {
            return true;
        }

        if (!string.Equals(candidate.Value.OperationId, request.OperationId, StringComparison.Ordinal)) {
            return false;
        }

        if (candidate.Value.PublicationFloor is not null || candidate.Value.PriorReplayFence is not null)
        {
            ProjectionReplayPublicationBinding? floor = candidate.Value.PublicationFloor;
            await SetPublicationStateAsync(ProjectionRebuildPromotionReceiptKey,
                new ProjectionRebuildPromotionReceipt(candidate.Value.PriorReplayFence?.OperationId ?? floor!.OperationId, null, null)
                { PublicationBinding = floor, PublicationFloor = floor, Completed = true,
                    SelectedReplayFence = candidate.Value.PriorReplayFence }).ConfigureAwait(false);
        }

        await RemovePublicationStateAsync(ProjectionRebuildCandidateKey).ConfigureAwait(false);
        await SaveRebuildStateAsync().ConfigureAwait(false);
        return true;
    }

    /// <inheritdoc/>
    public async Task<bool> RollbackProjectionAsync(ProjectionRebuildCandidateOperation request) {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.OperationId);
        await ClearPublicationCacheAsync().ConfigureAwait(false);
        ConditionalValue<ProjectionRebuildPromotionReceipt> receipt = await ReadPublicationStateAsync<ProjectionRebuildPromotionReceipt>(ProjectionRebuildPromotionReceiptKey)
            .ConfigureAwait(false);
        if (!receipt.HasValue) {
            return true;
        }

        if (!string.Equals(receipt.Value.OperationId, request.OperationId, StringComparison.Ordinal)) {
            return false;
        }

        if (receipt.Value.Completed) return false;
        ConditionalValue<ProjectionState> live = await ReadPublicationStateAsync<ProjectionState>(ProjectionStateKey).ConfigureAwait(false);
        if (!live.HasValue || live.Value is null || receipt.Value.PromotedState is not { } promoted
            || live.Value.ProjectionType != promoted.ProjectionType || live.Value.TenantId != promoted.TenantId
            || !live.Value.StateBytes.AsSpan().SequenceEqual(promoted.StateBytes)) return false;

        if (receipt.Value.PreviousState is null) {
            await RemovePublicationStateAsync(ProjectionStateKey).ConfigureAwait(false);
        }
        else {
            await SetPublicationStateAsync(ProjectionStateKey, receipt.Value.PreviousState).ConfigureAwait(false);
        }

        if (receipt.Value.PublicationFloor is not null || receipt.Value.PriorReplayFence is not null)
        {
            ProjectionReplayPublicationBinding? floor = receipt.Value.PublicationFloor;
            await SetPublicationStateAsync(ProjectionRebuildPromotionReceiptKey,
                new ProjectionRebuildPromotionReceipt(receipt.Value.PriorReplayFence?.OperationId ?? floor!.OperationId, null, null)
                { PublicationBinding = floor, PublicationFloor = floor, Completed = true,
                    SelectedReplayFence = receipt.Value.PriorReplayFence }).ConfigureAwait(false);
        }
        else await RemovePublicationStateAsync(ProjectionRebuildPromotionReceiptKey).ConfigureAwait(false);
        _ = await TryRemovePublicationStateAsync(ProjectionRebuildCandidateKey).ConfigureAwait(false);
        await SaveRebuildStateAsync().ConfigureAwait(false);
        return true;
    }

    /// <inheritdoc/>
    public async Task<bool> FinalizeProjectionAsync(ProjectionRebuildCandidateOperation request) {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.OperationId);
        await ClearPublicationCacheAsync().ConfigureAwait(false);
        ConditionalValue<ProjectionRebuildPromotionReceipt> receipt = await ReadPublicationStateAsync<ProjectionRebuildPromotionReceipt>(ProjectionRebuildPromotionReceiptKey)
            .ConfigureAwait(false);
        if (!receipt.HasValue) {
            return true;
        }

        if (!string.Equals(receipt.Value.OperationId, request.OperationId, StringComparison.Ordinal)) {
            return false;
        }

        if (receipt.Value.PublicationFloor is not null || receipt.Value.SelectedReplayFence is not null)
        {
            if (receipt.Value.Completed) return true;
            await SetPublicationStateAsync(ProjectionRebuildPromotionReceiptKey,
                receipt.Value with { PreviousState = null, PromotedState = null, Completed = true }).ConfigureAwait(false);
        }
        else await RemovePublicationStateAsync(ProjectionRebuildPromotionReceiptKey).ConfigureAwait(false);
        await SaveRebuildStateAsync().ConfigureAwait(false);
        return true;
    }

    /// <inheritdoc/>
    public async Task<ProjectionState?> ReadProjectionStateAsync() {
        await ClearPublicationCacheAsync().ConfigureAwait(false);
        ConditionalValue<ProjectionState> state = await ReadPublicationStateAsync<ProjectionState>(ProjectionStateKey)
            .ConfigureAwait(false);
        return state.HasValue ? state.Value : null;
    }

    private async Task SaveRebuildStateAsync()
    {
        try { await SavePublicationStateAsync().ConfigureAwait(false); }
        catch
        {
            await ClearPublicationCacheAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc/>
    protected override Task<QueryResult> ExecuteQueryAsync(QueryEnvelope envelope) =>
        ExecuteQueryAsync(envelope, CancellationToken.None);

    /// <inheritdoc/>
    protected override async Task<QueryResult> ExecuteQueryAsync(QueryEnvelope envelope, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(envelope);

        if (!string.IsNullOrWhiteSpace(envelope.Paging?.Cursor)) {
            return QueryResult.Failure(QueryAdapterFailureReason.InvalidCursor);
        }

        // Read last persisted state from DAPR actor state
        ConditionalValue<ProjectionState> result =
            await ReadPublicationStateAsync<ProjectionState>(ProjectionStateKey, cancellationToken)
                .ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        if (!result.HasValue) {
            Log.NoPersistedState(logger, Id.GetId());
            return QueryResult.Failure("No projection state available for this aggregate");
        }

        Log.PersistedStateReturned(logger, Id.GetId(), result.Value.ProjectionType);
        return QueryResult.FromPayload(result.Value.GetState(), result.Value.ProjectionType);
    }

    private static void ValidateState(ProjectionState state) {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(state.ProjectionType);
        ArgumentException.ThrowIfNullOrWhiteSpace(state.TenantId);
    }

    private static partial class Log {
        [LoggerMessage(
            EventId = 1090,
            Level = LogLevel.Debug,
            Message = "Projection state persisted: ActorId={ActorId}, ProjectionType={ProjectionType}, TenantId={TenantId}")]
        public static partial void ProjectionStatePersisted(ILogger logger, string actorId, string projectionType, string tenantId);

        [LoggerMessage(
            EventId = 1091,
            Level = LogLevel.Debug,
            Message = "No persisted projection state: ActorId={ActorId}")]
        public static partial void NoPersistedState(ILogger logger, string actorId);

        [LoggerMessage(
            EventId = 1092,
            Level = LogLevel.Debug,
            Message = "Returning persisted projection state: ActorId={ActorId}, ProjectionType={ProjectionType}")]
        public static partial void PersistedStateReturned(ILogger logger, string actorId, string projectionType);

        [LoggerMessage(
            EventId = 1093,
            Level = LogLevel.Warning,
            Message = "Projection change notification failed after persistence (fail-open). ActorId={ActorId}, ProjectionType={ProjectionType}, TenantId={TenantId}, ExceptionType={ExceptionType}")]
        public static partial void ProjectionChangeNotificationFailed(ILogger logger, string actorId, string projectionType, string tenantId, string exceptionType);
    }
}
