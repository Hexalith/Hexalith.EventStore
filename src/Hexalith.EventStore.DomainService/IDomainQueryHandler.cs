using Hexalith.EventStore.Contracts.Queries;

namespace Hexalith.EventStore.DomainService;

/// <summary>
/// Seam for serving a domain query as plain domain code. A domain module implements one handler per query
/// type instead of subclassing a projection actor and hand-writing a query-type <c>switch</c>. Handlers are
/// discovered and registered by <c>AddEventStoreDomainService</c> and dispatched by
/// <see cref="DomainQueryDispatcher"/> behind the SDK's <c>/query</c> endpoint (which the EventStore gateway
/// invokes via DAPR).
/// </summary>
/// <remarks>
/// The handler receives the authenticated <see cref="QueryEnvelope"/> (tenant, domain, query type, payload,
/// user id, optional entity id) and returns a <see cref="QueryResult"/>. Read-model access and pagination
/// cursors are provided by separate platform capabilities (Epic A8 read-model store, A9 cursor codec); until
/// those land a handler works from the envelope and whatever domain services it injects.
/// </remarks>
public interface IDomainQueryHandler {
    /// <summary>Gets the kebab-case domain this handler serves (matched against <see cref="QueryEnvelope.Domain"/>).</summary>
    string Domain { get; }

    /// <summary>Gets the query type discriminator this handler serves (matched against <see cref="QueryEnvelope.QueryType"/>).</summary>
    string QueryType { get; }

    /// <summary>
    /// Executes the query and returns its result.
    /// </summary>
    /// <param name="query">The authenticated query envelope.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The query result (use <see cref="QueryResult.FromPayload"/> / <see cref="QueryResult.Failure"/>).</returns>
    Task<QueryResult> ExecuteAsync(QueryEnvelope query, CancellationToken cancellationToken);
}
