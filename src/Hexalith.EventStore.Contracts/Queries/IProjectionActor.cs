namespace Hexalith.EventStore.Contracts.Queries;

/// <summary>
/// Implementation-neutral projection query method contract for generic query serving.
/// </summary>
/// <remarks>
/// EventStore routes <c>POST /api/v1/queries</c> to runtime adapters exposing this
/// method contract. DAPR actor hosts should mirror this method on their runtime-owned
/// actor interface in the hosting project; test fakes and adapter shims can implement
/// this interface without importing any DAPR package.
/// The actor ID uses a three-tier model where the first segment is <c>projectionType</c>
/// when supplied by the caller, otherwise <c>queryType</c>:
/// <c>{first}:{TenantId}:{EntityId}</c> for entity-scoped queries,
/// <c>{first}:{TenantId}:{Checksum}</c> for payload-scoped searches, and
/// <c>{first}:{TenantId}</c> for tenant-wide lists.
/// </remarks>
public interface IProjectionActor {
    /// <summary>
    /// Serves a projection query from a public query envelope.
    /// </summary>
    /// <param name="envelope">The query envelope containing routing metadata and UTF-8 JSON payload bytes.</param>
    /// <returns>The query result containing payload bytes or an adapter-edge failure.</returns>
    Task<QueryResult> QueryAsync(QueryEnvelope envelope);
}
