using System.Text.Json.Nodes;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Testing.Fakes;

namespace Hexalith.EventStore.DomainService.Tests;

internal sealed class OccupiedScratchBatchStore(InMemoryReadModelStore inner) : IReadModelBatchStore
{
    internal ReadModelBatch? OriginalBatch { get; private set; }

    public async Task<ReadModelBatchResult> ExecuteAsync(ReadModelBatch batch, CancellationToken cancellationToken = default)
    {
        bool interrupted = OriginalBatch is null && batch.Scope.BatchId.StartsWith("owner-scratch:", StringComparison.Ordinal);
        if (OriginalBatch is null && batch.Scope.BatchId.StartsWith("owner-scratch:", StringComparison.Ordinal))
        {
            OriginalBatch = batch;
            inner.SeedRaw(batch.Scope.StoreName, batch.Operations[0].Key, JsonNode.Parse(batch.Operations[0].CanonicalValue.Span)!);
        }
        ReadModelBatchResult result = await inner.ExecuteAsync(batch, cancellationToken);
        if (interrupted && result.ConflictKind == ReadModelBatchConflictKind.Optimistic)
            throw new InvalidOperationException("owning dispatcher exited after durable preflight conflict");
        return result;
    }
}
