using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Queries;

namespace Hexalith.EventStore.Sample.BlazorUI.Services;

/// <summary>
/// Platform-generic projection query client backed by the EventStore gateway.
/// </summary>
public sealed class EventStoreProjectionQueryClient(IEventStoreGatewayClient gateway)
{
    /// <summary>
    /// Submits a projection query through the EventStore gateway.
    /// </summary>
    public async Task<EventStoreQueryResult> GetAsync(
        string tenant,
        string domain,
        string aggregateId,
        string queryType,
        string? projectionType,
        string? entityId,
        string? ifNoneMatch,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);
        ArgumentException.ThrowIfNullOrWhiteSpace(queryType);

        var request = new SubmitQueryRequest(
            tenant,
            domain,
            aggregateId,
            queryType,
            projectionType,
            Payload: null,
            entityId);

        return await gateway
            .SubmitQueryAsync(request, ifNoneMatch, cancellationToken)
            .ConfigureAwait(false);
    }
}
