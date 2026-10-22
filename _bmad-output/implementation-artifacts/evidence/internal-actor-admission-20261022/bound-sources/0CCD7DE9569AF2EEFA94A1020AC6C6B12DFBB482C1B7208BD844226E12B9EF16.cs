using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

/// <summary>Persists the live counter detail read model through the coordinated batch store.</summary>
public sealed class LiveCounterDetailProjectionHandler(
    IReadModelBatchStore batchStore,
    LiveNamedProjectionFaultControl faultControl) : IAsyncDomainProjectionRebuildHandler {
    /// <inheritdoc/>
    public string Domain => "counter";

    /// <inheritdoc/>
    public string ProjectionType => "counter-detail";

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
        string key = $"{request.TenantId}:{request.Domain}:{request.AggregateId}:detail";
        string projectionVersion = request.Events.Max(static item => item.SequenceNumber).ToString(
            System.Globalization.CultureInfo.InvariantCulture);
        var value = new Dictionary<string, object> {
            ["eventCount"] = request.Events.Length,
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
        faultControl.RecordDetailInvocation();
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
