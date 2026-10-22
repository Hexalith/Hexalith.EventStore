using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Testing.Fakes;

namespace Hexalith.EventStore.DomainService.Tests;

/// <summary>Runs the historical pre-retained owner scratch shape through the production protocol.</summary>
internal sealed class LegacyScratchBatchStore(InMemoryReadModelStore store) : IReadModelBatchStore, IReadModelPendingBatchRecoveryStore
{
    public Task<ReadModelBatchResult> ExecuteAsync(ReadModelBatch batch, CancellationToken token = default)
        => store.ExecuteAsync(batch.Scope.BatchId.StartsWith("owner-scratch:", StringComparison.Ordinal)
            ? new ReadModelBatch(batch.Scope, batch.Operations) : batch, token);
    public Task<ReadModelPendingBatchRecoveryResult> RecoverPendingAsync(ReadModelBatchScope scope, string key,
        CancellationToken token = default) => store.RecoverPendingAsync(scope, key, token);
}
