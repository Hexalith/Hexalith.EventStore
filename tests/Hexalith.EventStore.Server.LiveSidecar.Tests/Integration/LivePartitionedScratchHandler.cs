using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Integration;

/// <summary>Domain-only partitioned handler used to qualify the production named dispatcher over Dapr.</summary>
internal class LivePartitionedScratchHandler : IAsyncDomainSharedProjectionPartitionedRebuildHandler
{
    public bool EpochEnabled => true;
    public string OrdinaryWriterId => "ordinary-dispatch";
    public string Domain => "widget";
    public string ProjectionType => "partitioned-widget";
    public string RebuildStoreName => "statestore";
    public SharedProjectionScope CreateScope(string tenantId) => new(RebuildStoreName, tenantId, Domain, ProjectionType, [OrdinaryWriterId]);
    public Task<ReadOnlyMemory<byte>> PreparePartitionAsync(DomainSharedProjectionRebuildIdentity identity, ProjectionRequest aggregateHistory, CancellationToken cancellationToken)
        => Task.FromResult((ReadOnlyMemory<byte>)aggregateHistory.Events.Last().Payload.ToArray());
    public Task<IReadOnlyList<ReadOnlyMemory<byte>>> PreparePartitionChunksAsync(DomainSharedProjectionRebuildIdentity identity, ProjectionRequest aggregateHistory, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<ReadOnlyMemory<byte>>>([aggregateHistory.Events.Last().Payload.ToArray()]);
    public Task<DomainProjectionRebuildPlan> FinalizePartitionsAsync(DomainSharedProjectionRebuildIdentity identity, IReadOnlyList<DomainSharedProjectionRebuildPartition> partitions, CancellationToken cancellationToken)
        => Task.FromResult(new DomainProjectionRebuildPlan(RebuildStoreName, partitions.GroupBy(static part => part.AggregateId)
            .Select(group => ReadModelBatchOperation.Write("row:" + group.Key, new JsonObject { ["count"] = (int)group.First().State.Span[0] }, ReadModelBatchConcurrency.LastWrite)).ToArray()));
    public Task<IReadOnlyList<ReadModelBatchOperation>> FoldCatchUpAsync(SharedProjectionDelivery delivery, long generation, SharedProjectionEpochCoordinator coordinator, CancellationToken cancellationToken)
    {
        ProjectionRequest request = JsonSerializer.Deserialize<ProjectionRequest>(delivery.CanonicalPayload, JsonSerializerOptions.Web)!;
        return Task.FromResult<IReadOnlyList<ReadModelBatchOperation>>([ReadModelBatchOperation.Write("row:" + request.AggregateId,
            new JsonObject { ["count"] = (int)request.Events.Last().Payload[0] }, ReadModelBatchConcurrency.LastWrite)]);
    }
    public Task<DomainProjectionHandlerResult> ProjectAsync(ProjectionRequest request, string dispatchId, CancellationToken cancellationToken)
        => Task.FromResult(DomainProjectionHandlerResult.AlreadyCompleted());
    public Task<DomainSharedProjectionRebuildCandidate> CreateEmptyCandidateAsync(DomainSharedProjectionRebuildIdentity identity, CancellationToken cancellationToken)
        => Task.FromResult(new DomainSharedProjectionRebuildCandidate([]));
    public Task<DomainSharedProjectionRebuildCandidate> AccumulateAsync(DomainSharedProjectionRebuildIdentity identity, DomainSharedProjectionRebuildCandidate candidate, ProjectionRequest aggregateHistory, CancellationToken cancellationToken)
        => throw new NotSupportedException();
    public Task<DomainProjectionRebuildPlan> FinalizeAsync(DomainSharedProjectionRebuildIdentity identity, DomainSharedProjectionRebuildCandidate candidate, CancellationToken cancellationToken)
        => throw new NotSupportedException();
}
