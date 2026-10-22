using Hexalith.EventStore.Contracts.Queries;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Hexalith.EventStore.ServiceDefaults.Authentication;

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
        CancellationToken cancellationToken = default)
        => await ExecuteCoreAsync(serviceProvider, query, query, cancellationToken).ConfigureAwait(false);

    internal static async Task<QueryResult> ExecuteWithAdministratorVerificationAsync(
        IServiceProvider serviceProvider, QueryEnvelope originalQuery, CancellationToken cancellationToken)
    {
        QueryEnvelope rebuilt = await DomainServiceAdministratorAssertions.RebuildAsync(serviceProvider, originalQuery, cancellationToken).ConfigureAwait(false);
        return await ExecuteCoreAsync(serviceProvider, rebuilt, originalQuery, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<QueryResult> ExecuteCoreAsync(
        IServiceProvider serviceProvider, QueryEnvelope query, QueryEnvelope originalQuery, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();

        HttpContext? http = serviceProvider.GetService<IHttpContextAccessor>()?.HttpContext;
        if (query.CallerAuthority is not null || http?.User.HasClaim(static claim => claim.Type == QueryCallerAuthorityBinding.DigestClaim) == true)
        {
            string gateway = serviceProvider.GetService<IConfiguration>()?["Authentication:QueryAuthority:GatewayWorkloadId"] ?? "eventstore";
            if (http is null || !QueryCallerAuthorityBinding.Verify(originalQuery, http.User, gateway)) return QueryResult.Failure("safe-denial");
            VerifiedQueryCaller.Set(http, query, originalQuery);
        }

        try
        {
            IDomainQueryHandler[] handlers = [.. serviceProvider
                .GetServices<IDomainQueryHandler>()
                .Where(h =>
                    string.Equals(h.Domain, query.Domain, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(h.QueryType, query.QueryType, StringComparison.OrdinalIgnoreCase))
                .OrderBy(h => h.GetType().FullName ?? h.GetType().Name, StringComparer.Ordinal)];

            DomainQueryHandlerRouteValidator.ThrowIfDuplicateRoutes(handlers);

            IDomainQueryHandler? handler = handlers.SingleOrDefault();

            QueryResult result = handler is null
                    ? QueryResult.Failure($"No query handler is registered for domain '{query.Domain}' query type '{query.QueryType}'.")
                    : await handler.ExecuteAsync(query, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return result;
        }
        finally
        {
            if (http is not null) VerifiedQueryCaller.Clear(http);
            cancellationToken.ThrowIfCancellationRequested();
        }
    }
}
