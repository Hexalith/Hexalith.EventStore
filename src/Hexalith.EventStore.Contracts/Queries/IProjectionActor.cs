using Dapr.Actors;

namespace Hexalith.EventStore.Contracts.Queries;

/// <summary>
/// Public DAPR actor interface for generic projection query serving.
/// </summary>
/// <remarks>
/// EventStore routes <c>POST /api/v1/queries</c> to actors implementing this
/// contract. The actor ID follows the documented three-tier model:
/// <c>{QueryType}:{TenantId}:{EntityId}</c> for entity-scoped queries,
/// <c>{QueryType}:{TenantId}:{Checksum}</c> for payload-scoped searches, and
/// <c>{QueryType}:{TenantId}</c> for tenant-wide lists.
/// </remarks>
public interface IProjectionActor : IActor {
    /// <summary>
    /// Serves a projection query from a public query envelope.
    /// </summary>
    /// <param name="envelope">The query envelope containing routing metadata and UTF-8 JSON payload bytes.</param>
    /// <returns>The query result containing payload bytes or an adapter-edge failure.</returns>
    Task<QueryResult> QueryAsync(QueryEnvelope envelope);
}
