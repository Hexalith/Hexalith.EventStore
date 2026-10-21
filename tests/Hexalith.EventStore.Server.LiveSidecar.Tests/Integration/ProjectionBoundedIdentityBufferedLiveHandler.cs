using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Integration;

/// <summary>Qualifies the unchanged buffered-owner, streamed-output contract consumed by Folders.</summary>
internal sealed class ProjectionBoundedIdentityBufferedLiveHandler : IAsyncDomainSharedProjectionStreamingRebuildHandler
{
    private readonly ProjectionBoundedIdentityLiveHandler _inner = new();
    public bool EpochEnabled => _inner.EpochEnabled;
    public string OrdinaryWriterId => _inner.OrdinaryWriterId;
    public string Domain => _inner.Domain;
    public string ProjectionType => _inner.ProjectionType;
    public string RebuildStoreName => _inner.RebuildStoreName;
    public SharedProjectionScope CreateScope(string tenantId) => _inner.CreateScope(tenantId);
    public Task<IReadOnlyList<ReadOnlyMemory<byte>>> PreparePartitionChunksAsync(DomainSharedProjectionRebuildIdentity identity, ProjectionRequest history, CancellationToken token)
        => Task.FromResult<IReadOnlyList<ReadOnlyMemory<byte>>>([JsonSerializer.SerializeToUtf8Bytes(new JsonObject
            { ["count"] = history.Events.LongLength, ["last"] = history.Events[^1].GlobalPosition })]);
    public IAsyncEnumerable<ReadModelBatchOperation> StreamFinalizedOperationsAsync(DomainSharedProjectionRebuildIdentity identity, IDomainSharedProjectionPartitionCursor partitions, CancellationToken token)
        => _inner.StreamFinalizedOperationsAsync(identity, partitions, token);
    public Task<IReadOnlyList<ReadModelBatchOperation>> FoldCatchUpAsync(SharedProjectionDelivery delivery, long generation, SharedProjectionEpochCoordinator coordinator, CancellationToken token)
        => _inner.FoldCatchUpAsync(delivery, generation, coordinator, token);
    public Task<DomainProjectionHandlerResult> ProjectAsync(ProjectionRequest request, string dispatchId, CancellationToken token)
        => _inner.ProjectAsync(request, dispatchId, token);
    public Task<ReadOnlyMemory<byte>> PreparePartitionAsync(DomainSharedProjectionRebuildIdentity identity, ProjectionRequest history, CancellationToken token)
        => _inner.PreparePartitionAsync(identity, history, token);
    public Task<DomainProjectionRebuildPlan> FinalizePartitionsAsync(DomainSharedProjectionRebuildIdentity identity, IReadOnlyList<DomainSharedProjectionRebuildPartition> partitions, CancellationToken token)
        => _inner.FinalizePartitionsAsync(identity, partitions, token);
    public Task<DomainSharedProjectionRebuildCandidate> CreateEmptyCandidateAsync(DomainSharedProjectionRebuildIdentity identity, CancellationToken token)
        => _inner.CreateEmptyCandidateAsync(identity, token);
    public Task<DomainSharedProjectionRebuildCandidate> AccumulateAsync(DomainSharedProjectionRebuildIdentity identity, DomainSharedProjectionRebuildCandidate candidate, ProjectionRequest history, CancellationToken token)
        => _inner.AccumulateAsync(identity, candidate, history, token);
    public Task<DomainProjectionRebuildPlan> FinalizeAsync(DomainSharedProjectionRebuildIdentity identity, DomainSharedProjectionRebuildCandidate candidate, CancellationToken token)
        => _inner.FinalizeAsync(identity, candidate, token);
}
