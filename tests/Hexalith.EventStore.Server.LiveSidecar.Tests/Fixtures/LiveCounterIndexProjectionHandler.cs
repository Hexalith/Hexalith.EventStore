using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

/// <summary>Persists the live counter index read model after an optional deterministic retryable failure.</summary>
public sealed class LiveCounterIndexProjectionHandler(
    IReadModelBatchStore batchStore,
    LiveNamedProjectionFaultControl faultControl) : IAsyncDomainProjectionRebuildHandler {
    /// <inheritdoc/>
    public string Domain => "counter";

    /// <inheritdoc/>
    public string ProjectionType => "counter-index";

    /// <inheritdoc/>
    public DomainProjectionRebuildSemantics RebuildSemantics => DomainProjectionRebuildSemantics.FullReplay;

    /// <inheritdoc/>
    public Task<DomainProjectionRebuildPlan> PrepareRebuildAsync(
        ProjectionRequest request,
        string operationId,
        CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        cancellationToken.ThrowIfCancellationRequested();
        string key = $"{request.TenantId}:{request.Domain}:{request.AggregateId}:index";
        string projectionVersion = request.Events.Max(static item => item.SequenceNumber).ToString(
            System.Globalization.CultureInfo.InvariantCulture);
        var value = new Dictionary<string, object> {
            ["aggregateId"] = request.AggregateId,
            ["dispatchId"] = operationId,
            ["projectionVersion"] = projectionVersion,
        };
        return Task.FromResult(new DomainProjectionRebuildPlan(
            "statestore",
            [ReadModelBatchOperation.Write(key, value, ReadModelBatchConcurrency.LastWrite)]));
    }

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
