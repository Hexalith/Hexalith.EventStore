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
    TimeProvider? timeProvider = null) : IProjectionLifecycleGateway {
    private static readonly TimeSpan DeliveryLeaseDuration = TimeSpan.FromMinutes(5);
    private readonly IActorProxyFactory _actorProxyFactory = actorProxyFactory
        ?? throw new ArgumentNullException(nameof(actorProxyFactory));
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    /// <inheritdoc/>
    public Task<bool> BeginRebuildAsync(
        AggregateIdentity identity,
        string projectionName,
        string operationId,
        CancellationToken cancellationToken = default) {
        ActorProxy proxy = CreateProxy(identity, projectionName);
        return proxy.InvokeMethodAsync<ProjectionRebuildLifecycleRequest, bool>(
            nameof(IProjectionLifecycleActor.BeginRebuildAsync),
            new ProjectionRebuildLifecycleRequest(operationId),
            cancellationToken);
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
    public Task<bool> BeginRebuildPromotionAsync(
        AggregateIdentity identity,
        string projectionName,
        string operationId,
        CancellationToken cancellationToken = default) {
        ActorProxy proxy = CreateProxy(identity, projectionName);
        return proxy.InvokeMethodAsync<ProjectionRebuildLifecycleRequest, bool>(
            nameof(IProjectionLifecycleActor.BeginRebuildPromotionAsync),
            new ProjectionRebuildLifecycleRequest(operationId),
            cancellationToken);
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
    public Task<ProjectionEraseAdmission> BeginEraseAsync(
        AggregateIdentity identity,
        string projectionName,
        string operationId,
        string manifestDigest,
        bool allowBegin,
        CancellationToken cancellationToken = default) {
        ActorProxy proxy = CreateProxy(identity, projectionName);
        return proxy.InvokeMethodAsync<ProjectionEraseBeginRequest, ProjectionEraseAdmission>(
            nameof(IProjectionLifecycleActor.BeginEraseAsync),
            new ProjectionEraseBeginRequest(operationId, manifestDigest, allowBegin),
            cancellationToken);
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

    private ActorProxy CreateProxy(AggregateIdentity identity, string projectionName) {
        ArgumentNullException.ThrowIfNull(identity);

        // Reuse the projection key reserved-char discipline (':', '\0', '|', CR, LF) so the
        // fourth actor-id segment cannot break the colon-delimited actor-id structure.
        ProjectionKeySegments.Validate(projectionName, nameof(projectionName));

        string actorId = $"{identity.TenantId}:{identity.Domain}:{identity.AggregateId}:{projectionName}";
        return _actorProxyFactory.Create(new ActorId(actorId), ProjectionLifecycleActor.ActorTypeName);
    }
}
