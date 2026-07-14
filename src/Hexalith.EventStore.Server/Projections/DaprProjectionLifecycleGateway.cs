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
internal sealed class DaprProjectionLifecycleGateway(IActorProxyFactory actorProxyFactory) : IProjectionLifecycleGateway {
    private readonly IActorProxyFactory _actorProxyFactory = actorProxyFactory
        ?? throw new ArgumentNullException(nameof(actorProxyFactory));

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
