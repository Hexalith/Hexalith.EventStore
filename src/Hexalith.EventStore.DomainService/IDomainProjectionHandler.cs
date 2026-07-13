using Hexalith.EventStore.Contracts.Projections;

namespace Hexalith.EventStore.DomainService;

/// <summary>
/// Seam for serving a domain's full-replay projection as plain domain code. A domain module implements one
/// handler per projection instead of hand-mapping a <c>/project</c> endpoint. Handlers are discovered and
/// registered by <c>AddEventStoreDomainService</c> and dispatched by <see cref="DomainProjectionDispatcher"/>
/// behind the SDK's <c>/project</c> endpoint (which the EventStore gateway's projection actor invokes via DAPR).
/// </summary>
/// <remarks>
/// This is the domain side of the platform's <b>stateless full-replay</b> projection model (Model a): the
/// handler receives the aggregate's complete event sequence in a <see cref="ProjectionRequest"/> and rebuilds
/// the read model from scratch, returning the current state as a <see cref="ProjectionResponse"/>. It holds no
/// state between calls and does not read or persist prior projection state — the gateway's projection actor
/// stores and serves the single returned read model. Persisted, incrementally-merged multi-read-model state is
/// a separate platform capability exposed through <see cref="IAsyncDomainProjectionHandler"/>. This legacy seam
/// remains unchanged for the version-1 <c>/project</c> protocol.
/// </remarks>
public interface IDomainProjectionHandler {
    /// <summary>Gets the kebab-case domain this handler serves (matched against <see cref="ProjectionRequest.Domain"/>).</summary>
    string Domain { get; }

    /// <summary>
    /// Projects the request's events onto a fresh read model and returns the current projection state.
    /// </summary>
    /// <param name="request">The projection request carrying the aggregate identity and full event sequence.</param>
    /// <returns>The rebuilt projection state.</returns>
    ProjectionResponse Project(ProjectionRequest request);
}
