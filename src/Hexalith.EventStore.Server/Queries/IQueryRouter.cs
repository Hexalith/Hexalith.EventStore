
using Hexalith.EventStore.Server.Pipeline.Queries;

namespace Hexalith.EventStore.Server.Queries;
/// <summary>
/// Routes queries to the correct projection actor based on canonical identity.
/// </summary>
public interface IQueryRouter {
    /// <summary>
    /// Routes a submit query to the appropriate projection actor.
    /// </summary>
    /// <param name="query">The query to route.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The routing result indicating success, payload, or not-found.</returns>
    Task<QueryRouterResult> RouteQueryAsync(SubmitQuery query, CancellationToken cancellationToken = default);
}
