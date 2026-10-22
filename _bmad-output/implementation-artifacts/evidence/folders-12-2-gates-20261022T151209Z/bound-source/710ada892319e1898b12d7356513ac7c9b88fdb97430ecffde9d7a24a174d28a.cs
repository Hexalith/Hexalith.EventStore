using System.Globalization;
using System.Text.Json;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Sample.Counter.Events;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

/// <summary>A generic full-replay handler with no source-guard or recovery-specific implementation.</summary>
public sealed class LiveReplayCounterProjectionHandler(IReadModelStore store, IReadModelBatchStore batches,
    LiveReplayCounterFaultControl faults) : IAsyncDomainProjectionRebuildHandler
{
    internal const string SourceDomain = "replay-counter";
    internal const string Route = "replay-counter-detail";
    internal const string Store = "statestore";

    /// <inheritdoc/>
    public string Domain => SourceDomain;

    /// <inheritdoc/>
    public string ProjectionType => Route;

    /// <inheritdoc/>
    public DomainProjectionRebuildSemantics RebuildSemantics => DomainProjectionRebuildSemantics.FullReplay;

    /// <inheritdoc/>
    public async Task<DomainProjectionRebuildPlan> PrepareRebuildAsync(ProjectionRequest request, string operationId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        faults.RecordPreparation();
        int count = 0;
        foreach (ProjectionEventDto item in request.Events)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _ = JsonSerializer.Deserialize<CounterIncremented>(item.Payload) ?? throw new InvalidOperationException("unreadable_event");
            count++;
        }
        string key = $"{request.TenantId}:{request.Domain}:{request.AggregateId}:{Route}";
        ReadModelEntry<LiveReplayCounterModel> prior = await store.GetAsync<LiveReplayCounterModel>(Store, key, cancellationToken).ConfigureAwait(false);
        ReadModelEntry<LiveReplayCounterModel> index = await store.GetAsync<LiveReplayCounterModel>(Store, key + "-index", cancellationToken).ConfigureAwait(false);
        var value = new LiveReplayCounterModel(count, request.Events[^1].SequenceNumber.ToString(CultureInfo.InvariantCulture));
        return new(Store,
        [ReadModelBatchOperation.Write(key, value, prior.Value is null ? ReadModelBatchConcurrency.CreateOnly : ReadModelBatchConcurrency.Match(prior.ETag!)),
            ReadModelBatchOperation.Write(key + "-index", value, index.Value is null ? ReadModelBatchConcurrency.CreateOnly : ReadModelBatchConcurrency.Match(index.ETag!))]);
    }

    /// <inheritdoc/>
    public async Task<DomainProjectionHandlerResult> ProjectAsync(ProjectionRequest request, string dispatchId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        DomainProjectionRebuildPlan plan = await PrepareRebuildAsync(request, dispatchId, cancellationToken).ConfigureAwait(false);
        return ReadModelBatchProjectionResultMapper.Map(await batches.ExecuteAsync(new(new(Store, request.TenantId,
            request.Domain, request.AggregateId, Route, dispatchId), plan.Operations), cancellationToken).ConfigureAwait(false));
    }
}
