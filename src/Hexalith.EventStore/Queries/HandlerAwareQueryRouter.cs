using System.Text.Json;

using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Server.Pipeline.Queries;
using Hexalith.EventStore.Server.Queries;

using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.Queries;

/// <summary>
/// <see cref="IQueryRouter"/> decorator implementing capability-declared query routing. When the target
/// domain advertises an <c>IDomainQueryHandler</c> for the query type (per
/// <see cref="IDomainQueryHandlerRegistry"/>), the query is invoked against the domain service's
/// <c>/query</c> endpoint via <see cref="IDomainQueryInvoker"/>; otherwise it delegates to the wrapped
/// projection-actor router. Handler-based queries do not participate in projection ETag caching (they
/// compute fresh results), so the returned <see cref="QueryRouterResult.ProjectionType"/> is left null.
/// </summary>
/// <remarks>
/// The wrapped <paramref name="inner"/> is the concrete projection-actor <c>QueryRouter</c>, supplied by the
/// DI factory (not resolved as <see cref="IQueryRouter"/>, which would resolve back to this decorator).
/// </remarks>
public sealed class HandlerAwareQueryRouter(
    IQueryRouter inner,
    IDomainQueryHandlerRegistry registry,
    IDomainQueryInvoker invoker,
    ILogger<HandlerAwareQueryRouter> logger) : IQueryRouter {
    /// <inheritdoc/>
    public async Task<QueryRouterResult> RouteQueryAsync(SubmitQuery query, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();

        bool handlerBased = await registry
            .SupportsQueryAsync(query.Domain, query.QueryType, cancellationToken)
            .ConfigureAwait(false);

        if (!handlerBased) {
            return await inner.RouteQueryAsync(query, cancellationToken).ConfigureAwait(false);
        }

        logger.LogDebug(
            "Routing query to domain handler endpoint: CorrelationId={CorrelationId}, Tenant={TenantId}, Domain={Domain}, QueryType={QueryType}, Stage=QueryHandlerRouting",
            query.CorrelationId,
            query.Tenant,
            query.Domain,
            query.QueryType);

        var envelope = new QueryEnvelope(
            query.Tenant,
            query.Domain,
            query.AggregateId,
            query.QueryType,
            query.Payload,
            query.CorrelationId,
            query.UserId,
            query.EntityId);

        QueryResult result = await invoker.InvokeAsync(envelope, cancellationToken).ConfigureAwait(false);

        if (!result.Success) {
            return new QueryRouterResult(Success: false, Payload: null, NotFound: false, ErrorMessage: result.ErrorMessage);
        }

        if (result.PayloadBytes is not { Length: > 0 }) {
            return new QueryRouterResult(Success: false, Payload: null, NotFound: false, ErrorMessage: "Domain query handler returned an empty payload.");
        }

        try {
            JsonElement payload = result.GetPayload();
            return new QueryRouterResult(Success: true, Payload: payload, NotFound: false, ProjectionType: result.ProjectionType);
        }
        catch (JsonException) {
            return new QueryRouterResult(Success: false, Payload: null, NotFound: false, ErrorMessage: "Domain query handler returned a malformed payload.");
        }
    }
}
