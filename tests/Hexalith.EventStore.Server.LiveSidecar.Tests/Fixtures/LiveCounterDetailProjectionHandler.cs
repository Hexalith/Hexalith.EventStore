using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

/// <summary>Persists the live counter detail read model through the coordinated batch store.</summary>
public sealed class LiveCounterDetailProjectionHandler(IReadModelBatchStore batchStore) : IAsyncDomainProjectionHandler {
    /// <inheritdoc/>
    public string Domain => "counter";

    /// <inheritdoc/>
    public string ProjectionType => "counter-detail";

    /// <inheritdoc/>
    public async Task<DomainProjectionHandlerResult> ProjectAsync(
        ProjectionRequest request,
        string dispatchId,
        CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(request);
        var scope = new ReadModelBatchScope(
            "statestore",
            request.TenantId,
            request.Domain,
            request.AggregateId,
            ProjectionType,
            dispatchId);
        string key = $"{request.TenantId}:{request.Domain}:{request.AggregateId}:detail";
        var value = new Dictionary<string, object> {
            ["eventCount"] = request.Events.Length,
            ["dispatchId"] = dispatchId,
        };
        ReadModelBatchResult result = await batchStore
            .ExecuteAsync(
                new ReadModelBatch(scope, [ReadModelBatchOperation.Write(key, value, ReadModelBatchConcurrency.LastWrite)]),
                cancellationToken)
            .ConfigureAwait(false);
        return ReadModelBatchProjectionResultMapper.Map(result);
    }
}
