
using Dapr.Actors;

namespace Hexalith.EventStore.Server.Actors;
/// <summary>
/// DAPR actor interface for projection read-model queries.
/// Application developers implement this interface to serve aggregate projections.
/// </summary>
/// <remarks>
/// The QueryRouter creates a proxy to this actor via
/// IActorProxyFactory.CreateActorProxy&lt;IProjectionActor&gt;(actorId, actorTypeName).
/// The actor ID uses a 3-tier routing model (format: "{QueryType}:{TenantId}[:{EntityId|Checksum}]"):
/// Tier 1 (EntityId-scoped): "{QueryType}:{TenantId}:{EntityId}",
/// Tier 2 (Payload-checksum): "{QueryType}:{TenantId}:{Checksum}",
/// Tier 3 (Tenant-wide): "{QueryType}:{TenantId}".
/// </remarks>
public interface IProjectionActor : IActor {
    /// <summary>
    /// Queries the projection actor for aggregate state.
    /// </summary>
    /// <param name="envelope">The query envelope containing routing and payload data.</param>
    /// <returns>The query result containing projection data.</returns>
    Task<QueryResult> QueryAsync(QueryEnvelope envelope);
}
