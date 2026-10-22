using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Integration;

/// <summary>Persists the process-restart proof through the supported named projection and rebuild seams.</summary>
/// <param name="store">The durable read-model batch store.</param>
/// <param name="stateKey">The isolated read-model key for this proof.</param>
internal sealed class ProjectionWatermarkProcessHandler(IReadModelBatchStore store, string stateKey)
    : IAsyncDomainProjectionRebuildHandler
{
    /// <inheritdoc/>
    public string Domain => "widget";

    /// <inheritdoc/>
    public string ProjectionType => "widget-watermark";

    /// <inheritdoc/>
    public DomainProjectionRebuildSemantics RebuildSemantics => DomainProjectionRebuildSemantics.FullReplay;

    /// <inheritdoc/>
    public async Task<DomainProjectionHandlerResult> ProjectAsync(
        ProjectionRequest request,
        string dispatchId,
        CancellationToken cancellationToken)
    {
        var batch = new ReadModelBatch(
            new ReadModelBatchScope(
                "statestore", request.TenantId, request.Domain, request.AggregateId, ProjectionType, dispatchId),
            [BuildWrite(request)]);
        ReadModelBatchResult result = await store.ExecuteAsync(batch, cancellationToken).ConfigureAwait(false);
        return ReadModelBatchProjectionResultMapper.Map(result);
    }

    /// <inheritdoc/>
    public Task<DomainProjectionRebuildPlan> PrepareRebuildAsync(
        ProjectionRequest request,
        string operationId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new DomainProjectionRebuildPlan("statestore", [BuildWrite(request)]));
    }

    private ReadModelBatchOperation BuildWrite(ProjectionRequest request)
    {
        long watermark = request.Events
            .Where(static value => value.GlobalPosition > 0)
            .Select(static value => value.GlobalPosition)
            .DefaultIfEmpty(0)
            .Max();
        var state = new ProjectionWatermarkProcessState(request.Events.Length, watermark);
        return ReadModelBatchOperation.Write(stateKey, state, ReadModelBatchConcurrency.LastWrite);
    }
}
