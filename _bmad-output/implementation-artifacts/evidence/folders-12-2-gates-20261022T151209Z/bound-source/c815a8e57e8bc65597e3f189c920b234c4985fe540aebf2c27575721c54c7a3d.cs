using Hexalith.EventStore.Contracts.Queries;

namespace Hexalith.EventStore.Queries;

/// <summary>
/// Invokes a domain service's <c>/query</c> endpoint (via DAPR) to serve a handler-based query.
/// The gateway counterpart of the command-side <c>IDomainServiceInvoker</c>.
/// </summary>
public interface IDomainQueryInvoker {
    /// <summary>
    /// Invokes the domain service that owns the query and returns its result.
    /// </summary>
    /// <param name="query">The query envelope to send to the domain's <c>/query</c> endpoint.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The domain's <see cref="QueryResult"/>.</returns>
    Task<QueryResult> InvokeAsync(QueryEnvelope query, CancellationToken cancellationToken = default);
}
