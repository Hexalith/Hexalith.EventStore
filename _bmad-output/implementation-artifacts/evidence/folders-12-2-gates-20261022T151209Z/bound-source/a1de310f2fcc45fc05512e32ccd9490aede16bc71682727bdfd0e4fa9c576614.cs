namespace Hexalith.EventStore.Server.Actors;

public partial class ProjectionLifecycleActor : IProjectionDeliveryLeaseActor
{
    /// <inheritdoc/>
    public async Task<bool> OwnsDeliveryLeaseAsync(ProjectionDeliveryLeaseProof request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.OperationId);
        ArgumentNullException.ThrowIfNull(request.Snapshot);
        await StateManager.ClearCacheAsync().ConfigureAwait(false);
        ProjectionLifecycleActorState state = await ReadStateAsync().ConfigureAwait(false);
        return state.Phase == ProjectionLifecyclePhase.Delivering
            && state.OperationId == request.OperationId && state.Revision == request.Snapshot.Revision
            && request.Snapshot.Phase == ProjectionLifecyclePhase.Delivering
            && state.DeliveryLeaseExpiresAtUtc > _timeProvider.GetUtcNow();
    }
}
