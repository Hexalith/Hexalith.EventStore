using Hexalith.EventStore.ServiceDefaults.Routing;

using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Queries;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>DAPR actor proxy implementation of operation-scoped rebuild staging.</summary>
internal sealed class DaprProjectionRebuildWriteGateway(IActorProxyFactory actorProxyFactory)
    : IProjectionRebuildWriteGateway, IProjectionRebuildInspectionGateway, IProjectionReplayPublicationGateway {
    /// <inheritdoc/>
    public Task<ProjectionRebuildInspection> StageReplayAsync(string actorId, ProjectionReplayPublicationRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var proxy = CreatePublicationProxy(actorId);
        return RouteCatalogRemoteCall.InvokeAsync(() => proxy.StageReplayAsync(request).WaitAsync(cancellationToken));
    }

    /// <inheritdoc/>
    public Task<ProjectionRebuildInspection> PromoteReplayAsync(string actorId, ProjectionReplayPublicationRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var proxy = CreatePublicationProxy(actorId);
        return RouteCatalogRemoteCall.InvokeAsync(() => proxy.PromoteReplayAsync(request).WaitAsync(cancellationToken));
    }

    /// <inheritdoc/>
    public Task<bool> FinalizeReplayAsync(string actorId, ProjectionReplayPublicationRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var proxy = CreatePublicationProxy(actorId);
        return RouteCatalogRemoteCall.InvokeAsync(() => proxy.FinalizeReplayAsync(request).WaitAsync(cancellationToken));
    }

    private IProjectionReplayPublicationActor CreatePublicationProxy(string actorId)
        => actorProxyFactory.CreateActorProxy<IProjectionReplayPublicationActor>(new ActorId(actorId), QueryRouter.ProjectionActorTypeName);

    /// <inheritdoc/>
    public Task<bool> ReadReplayCompletionAsync(string actorId, ProjectionReplayPublicationRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var proxy = CreatePublicationProxy(actorId);
        return RouteCatalogRemoteCall.InvokeAsync(() => proxy.ReadReplayCompletionAsync(request).WaitAsync(cancellationToken));
    }

    /// <inheritdoc/>
    public Task<bool> ReconcileBoundReplayCompletionAsync(string actorId, ProjectionReplayPublicationRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);
        IProjectionBoundReplayCompletionActor proxy = actorProxyFactory.CreateActorProxy<IProjectionBoundReplayCompletionActor>(
            new ActorId(actorId), QueryRouter.ProjectionActorTypeName);
        return RouteCatalogRemoteCall.InvokeAsync(() => proxy.ReconcileBoundReplayCompletionAsync(request).WaitAsync(cancellationToken));
    }

    /// <inheritdoc/>
    public async Task<ProjectionRebuildInspection> InspectAsync(string actorId, string operationId,
        CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        IProjectionRebuildInspectionActor proxy = actorProxyFactory.CreateActorProxy<IProjectionRebuildInspectionActor>(
            new ActorId(actorId), QueryRouter.ProjectionActorTypeName);
        return await RouteCatalogRemoteCall.InvokeAsync(() => proxy.InspectProjectionRebuildAsync(new(operationId)).WaitAsync(cancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task StageAsync(
        string actorId,
        ProjectionRebuildCandidate candidate,
        CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        IProjectionRebuildWriteActor proxy = CreateProxy(actorId);
        await RouteCatalogRemoteCall.InvokeAsync(() => proxy.StageProjectionAsync(candidate)).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<bool> PromoteAsync(
        string actorId,
        string operationId,
        CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        IProjectionRebuildWriteActor proxy = CreateProxy(actorId);
        return await RouteCatalogRemoteCall.InvokeAsync(() => proxy
            .PromoteProjectionAsync(new ProjectionRebuildCandidateOperation(operationId)))
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<bool> DiscardAsync(
        string actorId,
        string operationId,
        CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        IProjectionRebuildWriteActor proxy = CreateProxy(actorId);
        return await RouteCatalogRemoteCall.InvokeAsync(() => proxy
            .DiscardProjectionAsync(new ProjectionRebuildCandidateOperation(operationId)))
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<bool> RollbackAsync(
        string actorId,
        string operationId,
        CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        IProjectionRebuildWriteActor proxy = CreateProxy(actorId);
        return await RouteCatalogRemoteCall.InvokeAsync(() => proxy
            .RollbackProjectionAsync(new ProjectionRebuildCandidateOperation(operationId)))
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<bool> FinalizeAsync(
        string actorId,
        string operationId,
        CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        IProjectionRebuildWriteActor proxy = CreateProxy(actorId);
        return await RouteCatalogRemoteCall.InvokeAsync(() => proxy
            .FinalizeProjectionAsync(new ProjectionRebuildCandidateOperation(operationId)))
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<ProjectionState?> ReadAsync(
        string actorId,
        CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        IProjectionRebuildWriteActor proxy = CreateProxy(actorId);
        return await RouteCatalogRemoteCall.InvokeAsync(() => proxy.ReadProjectionStateAsync()).ConfigureAwait(false);
    }

    private IProjectionRebuildWriteActor CreateProxy(string actorId) {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);
        return actorProxyFactory.CreateActorProxy<IProjectionRebuildWriteActor>(
            new ActorId(actorId),
            QueryRouter.ProjectionActorTypeName);
    }
}
