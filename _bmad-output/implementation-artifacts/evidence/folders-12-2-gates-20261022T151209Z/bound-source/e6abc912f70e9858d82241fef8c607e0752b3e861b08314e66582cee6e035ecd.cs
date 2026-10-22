using System.Text.Json.Nodes;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Integration;

/// <summary>Uses the production streamed-owner transport and hash-page paths over the live store.</summary>
internal sealed class LiveStreamedPartitionedScratchHandler : LivePartitionedScratchHandler, IAsyncDomainSharedProjectionStreamedOwnerHandler
{
    public async IAsyncEnumerable<ReadOnlyMemory<byte>> StreamPartitionChunksAsync(DomainSharedProjectionRebuildIdentity identity,
        string aggregateId, IAsyncEnumerable<ProjectionEventDto> events,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken token)
    {
        byte[]? last = null;
        await foreach (var item in events.WithCancellation(token)) last = item.Payload;
        if (last is null) throw new InvalidOperationException("Missing original owner events.");
        for (int offset = 0; offset < last.Length; offset += 256_000)
            yield return last.AsMemory(offset, Math.Min(256_000, last.Length - offset));
    }

    public async IAsyncEnumerable<ReadModelBatchOperation> StreamFinalizedOperationsAsync(DomainSharedProjectionRebuildIdentity identity,
        IDomainSharedProjectionPartitionCursor partitions,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken token)
    {
        string? owner = null;
        await foreach (var chunk in partitions.ReadAllAsync(token))
        {
            if (chunk.AggregateId == owner) continue;
            owner = chunk.AggregateId;
            yield return ReadModelBatchOperation.Write("row:" + owner, new JsonObject { ["count"] = (int)chunk.State.Span[0] }, ReadModelBatchConcurrency.LastWrite);
        }
    }
}
