using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Queries;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>DAPR actor proxy implementation of operation-scoped rebuild staging.</summary>
internal sealed class DaprProjectionRebuildWriteGateway(IActorProxyFactory actorProxyFactory)
    : IProjectionRebuildWriteGateway {
    /// <inheritdoc/>
    public async Task StageAsync(
        string actorId,
        ProjectionRebuildCandidate candidate,
        CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        IProjectionRebuildWriteActor proxy = CreateProxy(actorId);
        await proxy.StageProjectionAsync(candidate).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<bool> PromoteAsync(
        string actorId,
        string operationId,
        CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        IProjectionRebuildWriteActor proxy = CreateProxy(actorId);
        return await proxy
            .PromoteProjectionAsync(new ProjectionRebuildCandidateOperation(operationId))
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<bool> DiscardAsync(
        string actorId,
        string operationId,
        CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        IProjectionRebuildWriteActor proxy = CreateProxy(actorId);
        return await proxy
            .DiscardProjectionAsync(new ProjectionRebuildCandidateOperation(operationId))
            .ConfigureAwait(false);
    }

    private IProjectionRebuildWriteActor CreateProxy(string actorId) {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);
        return actorProxyFactory.CreateActorProxy<IProjectionRebuildWriteActor>(
            new ActorId(actorId),
            QueryRouter.ProjectionActorTypeName);
    }
}
