using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

/// <summary>Persists the live counter index read model after an optional deterministic retryable failure.</summary>
public sealed class LiveCounterIndexProjectionHandler(
    IReadModelBatchStore batchStore,
    LiveNamedProjectionFaultControl faultControl) : IAsyncDomainProjectionHandler {
    /// <inheritdoc/>
    public string Domain => "counter";

    /// <inheritdoc/>
    public string ProjectionType => "counter-index";

    /// <inheritdoc/>
    public async Task<DomainProjectionHandlerResult> ProjectAsync(
        ProjectionRequest request,
        string dispatchId,
        CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(request);
        faultControl.RecordIndexInvocation();
        if (faultControl.FailIndex) {
            return DomainProjectionHandlerResult.Retryable(ProjectionDispatchReasonCodes.PartialRetry);
        }

        var scope = new ReadModelBatchScope(
            "statestore",
            request.TenantId,
            request.Domain,
            request.AggregateId,
            ProjectionType,
            dispatchId);
        string key = $"{request.TenantId}:{request.Domain}:{request.AggregateId}:index";
        var value = new Dictionary<string, object> {
            ["aggregateId"] = request.AggregateId,
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
