using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Testing.Fakes;

namespace Hexalith.EventStore.Client.Tests.Projections;

/// <summary>Pauses an immutable owner identity plan before its first durable manifest or marker exists.</summary>
internal sealed class DelayedCapturedIdentityBatchStore(InMemoryReadModelStore inner) : IReadModelBatchStore, IReadModelPendingBatchRecoveryStore
{
    internal TaskCompletionSource Planned { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal ReadModelBatch? Batch { get; private set; }
    public async Task<ReadModelBatchResult> ExecuteAsync(ReadModelBatch batch, CancellationToken cancellationToken = default)
    {
        if (Batch is null && batch.Scope.BatchId.StartsWith("identity-node:", StringComparison.Ordinal))
        {
            Batch = batch;
            Planned.SetResult();
            await Release.Task.WaitAsync(cancellationToken);
        }
        return await inner.ExecuteAsync(batch, cancellationToken);
    }
    public Task<ReadModelPendingBatchRecoveryResult> RecoverPendingAsync(ReadModelBatchScope scope, string key, CancellationToken cancellationToken = default)
        => inner.RecoverPendingAsync(scope, key, cancellationToken);
}
