
using Dapr.Actors.Runtime;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Server.Queries;

using Microsoft.Extensions.Logging;

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
    : CachingProjectionActor(host, eTagService, logger), IProjectionWriteActor {
    internal const string ProjectionStateKey = "projection-state";

    /// <inheritdoc/>
    public async Task UpdateProjectionAsync(ProjectionState state) {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(state.ProjectionType);
        ArgumentException.ThrowIfNullOrWhiteSpace(state.TenantId);

        // 1. Persist to DAPR actor state
        await StateManager.SetStateAsync(ProjectionStateKey, state).ConfigureAwait(false);
        await StateManager.SaveStateAsync().ConfigureAwait(false);

        Log.ProjectionStatePersisted(logger, Id.GetId(), state.ProjectionType, state.TenantId);

        // 2. Regenerate ETag + broadcast SignalR (fail-open on notifier path)
        try {
            await projectionChangeNotifier.NotifyProjectionChangedAsync(
                state.ProjectionType,
                state.TenantId)
                .ConfigureAwait(false);
        }
        catch (Exception ex) {
            Log.ProjectionChangeNotificationFailed(logger, Id.GetId(), state.ProjectionType, state.TenantId, ex.GetType().Name);
        }
    }

    /// <inheritdoc/>
    protected override async Task<QueryResult> ExecuteQueryAsync(QueryEnvelope envelope) {
        // Read last persisted state from DAPR actor state
        ConditionalValue<ProjectionState> result =
            await StateManager.TryGetStateAsync<ProjectionState>(ProjectionStateKey)
                .ConfigureAwait(false);

        if (!result.HasValue) {
            Log.NoPersistedState(logger, Id.GetId());
            return new QueryResult(false, default, "No projection state available for this aggregate");
        }

        Log.PersistedStateReturned(logger, Id.GetId(), result.Value.ProjectionType);
        return new QueryResult(true, result.Value.State, ProjectionType: result.Value.ProjectionType);
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
