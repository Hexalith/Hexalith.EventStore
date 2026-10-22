using System.Text.Json;

using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Pipeline.Queries;
using Hexalith.EventStore.Server.Queries;

namespace Hexalith.EventStore.ProviderVerification;

internal sealed class StatefulQueryRouter(ProviderStateCoordinator coordinator) : IQueryRouter
{
    private static readonly JsonElement _emptyPayload = JsonSerializer.SerializeToElement(Array.Empty<object>());
    private static readonly JsonElement _rowPayload = JsonSerializer.SerializeToElement(new[]
    {
        new { id = "order-1", status = "Pending" },
    });

    public Task<QueryRouterResult> RouteQueryAsync(
        SubmitQuery query,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        QueryRouterResult result = SupportedProviderStates.RequireActive(coordinator) switch
        {
            "query-empty-result" => Success(_emptyPayload, 0),
            "query-malformed-payload" => new(false, null, false, QueryAdapterFailureReason.InvalidEnvelope),
            "query-forbidden" => new(false, null, false, QueryAdapterFailureReason.Forbidden),
            "query-not-found" => new(false, null, true),
            "query-rate-limited" => throw new BackpressureExceededException(
                query.CorrelationId,
                query.Tenant,
                query.Domain,
                query.AggregateId,
                2,
                1),
            "query-fresh-data" or "query-etag-match" or "query-etag-no-cache"
                or "query-auth-tenant" => Success(_rowPayload, 1),
            "query-large-valid-metadata" => Success(_rowPayload, 73),
            _ => throw new InvalidOperationException("provider-query-state-unsupported"),
        };
        return Task.FromResult(result);
    }

    private static QueryRouterResult Success(JsonElement payload, long totalCount)
        => new(
            true,
            payload,
            false,
            ProjectionType: "orders",
            Metadata: new QueryResponseMetadata(Paging: new QueryPagingMetadata(
                PageSize: 25,
                Offset: 0,
                TotalCount: totalCount,
                HasMore: totalCount > 25))
            {
                Provenance = QueryResponseProvenance.ProjectionBacked,
                Lifecycle = ProjectionLifecycleState.Current,
            });
}
