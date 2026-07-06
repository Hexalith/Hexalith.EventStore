using Hexalith.EventStore.Contracts.Queries;

using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.EventStore.DomainService;

/// <summary>
/// Routes a <see cref="QueryEnvelope"/> to the registered <see cref="IDomainQueryHandler"/> whose
/// <see cref="IDomainQueryHandler.Domain"/> and <see cref="IDomainQueryHandler.QueryType"/> match. Backs the
/// SDK's <c>/query</c> endpoint, mirroring how <see cref="DomainServiceRequestRouter"/> backs <c>/process</c>.
/// </summary>
public static class DomainQueryDispatcher {
    /// <summary>
    /// Executes a query by dispatching it to the matching domain query handler.
    /// </summary>
    /// <param name="serviceProvider">The scoped request service provider.</param>
    /// <param name="query">The query envelope to dispatch.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>
    /// The handler's <see cref="QueryResult"/>, or <see cref="QueryResult.Failure"/> when no handler is
    /// registered for the envelope's domain and query type.
    /// </returns>
    public static async Task<QueryResult> ExecuteAsync(
        IServiceProvider serviceProvider,
        QueryEnvelope query,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(query);

        IDomainQueryHandler[] handlers = [.. serviceProvider
            .GetServices<IDomainQueryHandler>()
            .Where(h =>
                string.Equals(h.Domain, query.Domain, StringComparison.OrdinalIgnoreCase)
                && string.Equals(h.QueryType, query.QueryType, StringComparison.OrdinalIgnoreCase))
            .OrderBy(h => h.GetType().FullName ?? h.GetType().Name, StringComparer.Ordinal)];

        DomainQueryHandlerRouteValidator.ThrowIfDuplicateRoutes(handlers);

        IDomainQueryHandler? handler = handlers.SingleOrDefault();

        return handler is null
            ? QueryResult.Failure($"No query handler is registered for domain '{query.Domain}' query type '{query.QueryType}'.")
            : await handler.ExecuteAsync(query, cancellationToken).ConfigureAwait(false);
    }
}
