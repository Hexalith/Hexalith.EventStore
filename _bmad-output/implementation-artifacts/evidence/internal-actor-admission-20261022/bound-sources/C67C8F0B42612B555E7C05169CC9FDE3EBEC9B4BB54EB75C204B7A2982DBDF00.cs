
using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Contracts.Queries;

namespace Hexalith.EventStore.Server.Queries;

/// <summary>
/// Default <see cref="IProjectionActorInvoker"/> implementation that creates a weak DAPR
/// <see cref="ActorProxy"/> via <see cref="IActorProxyFactory.Create(ActorId,string,ActorProxyOptions)"/>
/// and invokes <c>QueryAsync</c> by name so the weak/JSON invocation path is initialized
/// with the request-scope <see cref="CancellationToken"/>.
/// </summary>
/// <remarks>
/// The DAPR strongly typed dispatch proxy returned by
/// <see cref="IActorProxyFactory.CreateActorProxy{TActorInterface}(ActorId,string,ActorProxyOptions)"/>
/// inherits from <see cref="ActorProxy"/> at runtime but does not initialize the weak/JSON
/// invocation state. Calling <c>ActorProxy.InvokeMethodAsync&lt;TRequest,TResponse&gt;</c> on
/// such a dispatch proxy throws <see cref="NullReferenceException"/>. This implementation
/// therefore creates the weak proxy directly.
/// </remarks>
internal sealed class DefaultProjectionActorInvoker(IActorProxyFactory actorProxyFactory) : IProjectionActorInvoker {
    private readonly IActorProxyFactory _actorProxyFactory = actorProxyFactory
        ?? throw new ArgumentNullException(nameof(actorProxyFactory));

    /// <inheritdoc/>
    public Task<QueryResult> InvokeAsync(
        string actorId,
        string actorTypeName,
        QueryEnvelope envelope,
        CancellationToken cancellationToken) {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);
        ArgumentException.ThrowIfNullOrWhiteSpace(actorTypeName);
        ArgumentNullException.ThrowIfNull(envelope);
        cancellationToken.ThrowIfCancellationRequested();

        ActorProxy proxy = _actorProxyFactory.Create(new ActorId(actorId), actorTypeName);
        return proxy.InvokeMethodAsync<QueryEnvelope, QueryResult>(
            nameof(IProjectionActor.QueryAsync),
            envelope,
            cancellationToken);
    }
}
