using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Integration;

/// <summary>Domain-only fold that consumes every original event and materializes every original owner.</summary>
internal sealed class ProjectionBoundedIdentityLiveHandler : IAsyncDomainSharedProjectionStreamedOwnerHandler
{
    public bool EpochEnabled => true;
    public string OrdinaryWriterId => "ordinary-dispatch";
    public string Domain => "widget";
    public string ProjectionType => "bounded-widget";
    public string RebuildStoreName => "statestore";
    public SharedProjectionScope CreateScope(string tenantId) => new(RebuildStoreName, tenantId, Domain, ProjectionType, [OrdinaryWriterId]);
    public async IAsyncEnumerable<ReadOnlyMemory<byte>> StreamPartitionChunksAsync(DomainSharedProjectionRebuildIdentity identity,
        string aggregateId, IAsyncEnumerable<ProjectionEventDto> events,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken token)
    {
        long count = 0, last = 0;
        await foreach (var item in events.WithCancellation(token)) { count++; last = item.GlobalPosition; }
        yield return JsonSerializer.SerializeToUtf8Bytes(new JsonObject { ["count"] = count, ["last"] = last });
    }
    public async IAsyncEnumerable<ReadModelBatchOperation> StreamFinalizedOperationsAsync(DomainSharedProjectionRebuildIdentity identity,
        IDomainSharedProjectionPartitionCursor partitions,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken token)
    {
        await foreach (var part in partitions.ReadAllAsync(token))
            yield return ReadModelBatchOperation.Write("row:" + part.AggregateId, JsonNode.Parse(part.State.Span)!, ReadModelBatchConcurrency.LastWrite);
    }
    public Task<IReadOnlyList<ReadModelBatchOperation>> FoldCatchUpAsync(SharedProjectionDelivery delivery, long generation,
        SharedProjectionEpochCoordinator coordinator, CancellationToken token)
    {
        var request = JsonSerializer.Deserialize<ProjectionRequest>(delivery.CanonicalPayload, JsonSerializerOptions.Web)!;
        return Task.FromResult<IReadOnlyList<ReadModelBatchOperation>>([ReadModelBatchOperation.Write("row:" + request.AggregateId,
            new JsonObject { ["count"] = request.Events[^1].SequenceNumber, ["last"] = request.Events[^1].GlobalPosition }, ReadModelBatchConcurrency.LastWrite)]);
    }
    public Task<DomainProjectionHandlerResult> ProjectAsync(ProjectionRequest request, string dispatchId, CancellationToken token)
        => Task.FromResult(DomainProjectionHandlerResult.AlreadyCompleted());
    public Task<ReadOnlyMemory<byte>> PreparePartitionAsync(DomainSharedProjectionRebuildIdentity identity, ProjectionRequest history, CancellationToken token)
        => throw new InvalidOperationException("Buffered original owner history is forbidden in this qualification.");
    public Task<IReadOnlyList<ReadOnlyMemory<byte>>> PreparePartitionChunksAsync(DomainSharedProjectionRebuildIdentity identity, ProjectionRequest history, CancellationToken token)
        => throw new InvalidOperationException("Buffered original owner history is forbidden in this qualification.");
    public Task<DomainProjectionRebuildPlan> FinalizePartitionsAsync(DomainSharedProjectionRebuildIdentity identity, IReadOnlyList<DomainSharedProjectionRebuildPartition> partitions, CancellationToken token)
        => throw new InvalidOperationException("Buffered finalization is forbidden in this qualification.");
    public Task<DomainSharedProjectionRebuildCandidate> CreateEmptyCandidateAsync(DomainSharedProjectionRebuildIdentity identity, CancellationToken token)
        => Task.FromResult(new DomainSharedProjectionRebuildCandidate([]));
    public Task<DomainSharedProjectionRebuildCandidate> AccumulateAsync(DomainSharedProjectionRebuildIdentity identity, DomainSharedProjectionRebuildCandidate candidate, ProjectionRequest history, CancellationToken token)
        => throw new NotSupportedException();
    public Task<DomainProjectionRebuildPlan> FinalizeAsync(DomainSharedProjectionRebuildIdentity identity, DomainSharedProjectionRebuildCandidate candidate, CancellationToken token)
        => throw new NotSupportedException();
}
