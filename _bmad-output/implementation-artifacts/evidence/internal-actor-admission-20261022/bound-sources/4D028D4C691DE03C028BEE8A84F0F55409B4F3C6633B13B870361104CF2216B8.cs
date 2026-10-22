
using Hexalith.EventStore.Contracts.Queries;

namespace Hexalith.EventStore.Server.Queries;

/// <summary>
/// Internal test seam for invoking a generic projection actor through the DAPR weak
/// <see cref="Dapr.Actors.Client.ActorProxy"/> path with a per-call <see cref="CancellationToken"/>.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Dapr.Actors.Client.ActorProxy"/> has an internal constructor and cannot be
/// substituted directly by NSubstitute, so the weak invocation path is wrapped behind this
/// interface. Production code uses <c>DefaultProjectionActorInvoker</c>, which calls
/// <see cref="Dapr.Actors.Client.IActorProxyFactory.Create(Dapr.Actors.ActorId,string,Dapr.Actors.Client.ActorProxyOptions)"/>
/// and then <see cref="Dapr.Actors.Client.ActorProxy.InvokeMethodAsync{TRequest,TResponse}(string,TRequest,CancellationToken)"/>.
/// </para>
/// <para>
/// This seam is intentionally internal to the Server assembly. It is not part of any public
/// contract and must not be exposed via the Contracts package.
/// </para>
/// </remarks>
internal interface IProjectionActorInvoker {
    /// <summary>
    /// Invokes the generic projection actor's <c>QueryAsync</c> method through the DAPR weak
    /// proxy path, carrying the supplied <paramref name="cancellationToken"/> into the
    /// DAPR invocation.
    /// </summary>
    /// <param name="actorId">Derived projection actor ID.</param>
    /// <param name="actorTypeName">Registered DAPR actor type name.</param>
    /// <param name="envelope">Public query envelope to deliver to the actor.</param>
    /// <param name="cancellationToken">Per-call cancellation token.</param>
    /// <returns>The actor's <see cref="QueryResult"/>.</returns>
    Task<QueryResult> InvokeAsync(
        string actorId,
        string actorTypeName,
        QueryEnvelope envelope,
        CancellationToken cancellationToken);
}
