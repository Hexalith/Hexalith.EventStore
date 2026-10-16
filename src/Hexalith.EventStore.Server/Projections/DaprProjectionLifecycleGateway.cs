using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Actors;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>
/// DAPR-backed <see cref="IProjectionLifecycleGateway"/> that composes the per
/// (tenant, domain, aggregate, projection) actor id and invokes <see cref="ProjectionLifecycleActor"/>
/// through the weak/JSON proxy path (mirroring <c>DefaultProjectionActorInvoker</c>). The strongly
/// typed dispatch proxy from <see cref="IActorProxyFactory.CreateActorProxy{TActorInterface}(ActorId,string,ActorProxyOptions)"/>
/// does not initialize the weak/JSON invocation state and would throw
/// <see cref="NullReferenceException"/> on <c>InvokeMethodAsync&lt;TRequest,TResponse&gt;</c>, so the
/// weak proxy is created directly.
/// </summary>
internal sealed class DaprProjectionLifecycleGateway(
    IActorProxyFactory actorProxyFactory,
    TimeProvider? timeProvider = null,
    IProjectionDeliveryStateStore? deliveryStates = null) : IProjectionLifecycleGateway {
    private static readonly TimeSpan DeliveryLeaseDuration = TimeSpan.FromMinutes(5);
    private readonly IActorProxyFactory _actorProxyFactory = actorProxyFactory
        ?? throw new ArgumentNullException(nameof(actorProxyFactory));
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    /// <inheritdoc/>
    public Task<bool> OwnsDeliveryWriteAsync(AggregateIdentity identity, string projectionName, string operationId,
        ProjectionLifecycleSnapshot snapshot, CancellationToken cancellationToken = default)
        => CreateProxy(identity, projectionName).InvokeMethodAsync<ProjectionDeliveryLeaseProof, bool>(
            nameof(IProjectionDeliveryLeaseActor.OwnsDeliveryLeaseAsync), new(operationId, snapshot), cancellationToken);

    /// <inheritdoc/>
    public async Task<bool> BeginRebuildAsync(
        AggregateIdentity identity,
        string projectionName,
        string operationId,
        CancellationToken cancellationToken = default) {
        if (await HasDeliveryFenceAsync(identity, projectionName, cancellationToken).ConfigureAwait(false)) return false;
        ActorProxy proxy = CreateProxy(identity, projectionName);
        bool admitted = await proxy.InvokeMethodAsync<ProjectionRebuildLifecycleRequest, bool>(
            nameof(IProjectionLifecycleActor.BeginRebuildAsync),
            new ProjectionRebuildLifecycleRequest(operationId),
            cancellationToken).ConfigureAwait(false);
        if (!admitted || !await HasDeliveryFenceAsync(identity, projectionName, cancellationToken).ConfigureAwait(false)) return admitted;
        // The second read closes the precheck/acquire window. No selection effects have started;
        // release only this competing operation, leaving the pending delivery fence intact.
        await CompleteRebuildAsync(identity, projectionName, operationId, cancellationToken).ConfigureAwait(false);
        return false;
    }

    /// <inheritdoc/>
    public Task<bool> CompleteRebuildAsync(
        AggregateIdentity identity,
        string projectionName,
        string operationId,
        CancellationToken cancellationToken = default) {
        ActorProxy proxy = CreateProxy(identity, projectionName);
        return proxy.InvokeMethodAsync<ProjectionRebuildLifecycleRequest, bool>(
            nameof(IProjectionLifecycleActor.CompleteRebuildAsync),
            new ProjectionRebuildLifecycleRequest(operationId),
            cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> BeginRebuildPromotionAsync(
        AggregateIdentity identity,
        string projectionName,
        string operationId,
        CancellationToken cancellationToken = default) {
        if (await HasDeliveryFenceAsync(identity, projectionName, cancellationToken).ConfigureAwait(false)) return false;
        ActorProxy proxy = CreateProxy(identity, projectionName);
        return await proxy.InvokeMethodAsync<ProjectionRebuildLifecycleRequest, bool>(
            nameof(IProjectionLifecycleActor.BeginRebuildPromotionAsync),
            new ProjectionRebuildLifecycleRequest(operationId),
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public Task<bool> CompleteRebuildPromotionAsync(
        AggregateIdentity identity,
        string projectionName,
        string operationId,
        CancellationToken cancellationToken = default) {
        ActorProxy proxy = CreateProxy(identity, projectionName);
        return proxy.InvokeMethodAsync<ProjectionRebuildLifecycleRequest, bool>(
            nameof(IProjectionLifecycleActor.CompleteRebuildPromotionAsync),
            new ProjectionRebuildLifecycleRequest(operationId),
            cancellationToken);
    }

    /// <inheritdoc/>
    public Task<ProjectionLifecyclePhase> ReadPhaseAsync(
        AggregateIdentity identity,
        string projectionName,
        CancellationToken cancellationToken = default) {
        ActorProxy proxy = CreateProxy(identity, projectionName);
        return proxy.InvokeMethodAsync<ProjectionLifecyclePhase>(
            nameof(IProjectionLifecycleActor.ReadPhaseAsync),
            cancellationToken);
    }

    /// <inheritdoc/>
    public Task<ProjectionLifecycleSnapshot> ReadSnapshotAsync(
        AggregateIdentity identity,
        string projectionName,
        CancellationToken cancellationToken = default) {
        ActorProxy proxy = CreateProxy(identity, projectionName);
        return proxy.InvokeMethodAsync<ProjectionLifecycleSnapshot>(
            nameof(IProjectionLifecycleActor.ReadSnapshotAsync),
            cancellationToken);
    }

    /// <inheritdoc/>
    public Task<bool> BeginDeliveryWriteAsync(
        AggregateIdentity identity,
        string projectionName,
        string operationId,
        CancellationToken cancellationToken = default) {
        ActorProxy proxy = CreateProxy(identity, projectionName);
        return proxy.InvokeMethodAsync<ProjectionDeliveryLifecycleRequest, bool>(
            nameof(IProjectionLifecycleActor.BeginDeliveryWriteAsync),
            new ProjectionDeliveryLifecycleRequest(
                operationId,
                _timeProvider.GetUtcNow() + DeliveryLeaseDuration),
            cancellationToken);
    }

    /// <inheritdoc/>
    public Task<bool> CompleteDeliveryWriteAsync(
        AggregateIdentity identity,
        string projectionName,
        string operationId,
        CancellationToken cancellationToken = default) {
        ActorProxy proxy = CreateProxy(identity, projectionName);
        return proxy.InvokeMethodAsync<ProjectionDeliveryLifecycleRequest, bool>(
            nameof(IProjectionLifecycleActor.CompleteDeliveryWriteAsync),
            new ProjectionDeliveryLifecycleRequest(operationId),
            cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> TryAdmitDeliveryWriteAsync(AggregateIdentity identity, string projectionName, CancellationToken cancellationToken = default) {
        ActorProxy proxy = CreateProxy(identity, projectionName);
        ProjectionDeliveryAdmission admission = await proxy
            .InvokeMethodAsync<ProjectionDeliveryAdmission>(
                nameof(IProjectionLifecycleActor.TryAdmitDeliveryWriteAsync),
                cancellationToken)
            .ConfigureAwait(false);
        return admission.Admitted;
    }

    /// <inheritdoc/>
    public async Task<ProjectionEraseAdmission> BeginEraseAsync(
        AggregateIdentity identity,
        string projectionName,
        string operationId,
        string manifestDigest,
        bool allowBegin,
        CancellationToken cancellationToken = default) {
        if (await HasDeliveryFenceAsync(identity, projectionName, cancellationToken).ConfigureAwait(false))
            return new(ProjectionEraseAdmissionKind.BeginNotAllowed, new Dictionary<string, string>(StringComparer.Ordinal));
        ActorProxy proxy = CreateProxy(identity, projectionName);
        ProjectionEraseAdmission admitted = await proxy.InvokeMethodAsync<ProjectionEraseBeginRequest, ProjectionEraseAdmission>(
            nameof(IProjectionLifecycleActor.BeginEraseAsync),
            new ProjectionEraseBeginRequest(operationId, manifestDigest, allowBegin),
            cancellationToken).ConfigureAwait(false);
        if (admitted.Kind is not ProjectionEraseAdmissionKind.Admitted and not ProjectionEraseAdmissionKind.Resume
            || !await HasDeliveryFenceAsync(identity, projectionName, cancellationToken).ConfigureAwait(false)) return admitted;
        await CompleteEraseAsync(identity, projectionName, operationId, cancellationToken).ConfigureAwait(false);
        return new(ProjectionEraseAdmissionKind.BeginNotAllowed, new Dictionary<string, string>(StringComparer.Ordinal));
    }

    /// <inheritdoc/>
    public Task<bool> RecordTargetOutcomeAsync(AggregateIdentity identity, string projectionName, string operationId, string targetKey, string outcome, CancellationToken cancellationToken = default) {
        ActorProxy proxy = CreateProxy(identity, projectionName);
        return proxy.InvokeMethodAsync<ProjectionTargetOutcomeRequest, bool>(
            nameof(IProjectionLifecycleActor.RecordTargetOutcomeAsync),
            new ProjectionTargetOutcomeRequest(operationId, targetKey, outcome),
            cancellationToken);
    }

    /// <inheritdoc/>
    public Task<bool> CompleteEraseAsync(AggregateIdentity identity, string projectionName, string operationId, CancellationToken cancellationToken = default) {
        ActorProxy proxy = CreateProxy(identity, projectionName);
        return proxy.InvokeMethodAsync<ProjectionEraseCompleteRequest, bool>(
            nameof(IProjectionLifecycleActor.CompleteEraseAsync),
            new ProjectionEraseCompleteRequest(operationId),
            cancellationToken);
    }

    private async Task<bool> HasDeliveryFenceAsync(AggregateIdentity identity, string projectionName, CancellationToken cancellationToken)
    {
        if (deliveryStates is null) return false;
        ProjectionDeliveryStateReadResult read = await deliveryStates.ReadAsync(identity, projectionName, cancellationToken).ConfigureAwait(false);
        return read.State?.IdleBindingPending is not null
            || read.State?.ActiveReservation?.GuardBinding is not null;
    }

    private ActorProxy CreateProxy(AggregateIdentity identity, string projectionName) {
        ArgumentNullException.ThrowIfNull(identity);

        // Reuse the projection key reserved-char discipline (':', '\0', '|', CR, LF) so the
        // fourth actor-id segment cannot break the colon-delimited actor-id structure.
        ProjectionKeySegments.Validate(projectionName, nameof(projectionName));

        string actorId = $"{identity.TenantId}:{identity.Domain}:{identity.AggregateId}:{projectionName}";
        return _actorProxyFactory.Create(new ActorId(actorId), ProjectionLifecycleActor.ActorTypeName);
    }
}
