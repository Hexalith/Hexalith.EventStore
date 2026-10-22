using Dapr.Client;
using Hexalith.EventStore.Client.Projections;
using Microsoft.Extensions.Logging.Abstractions;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Integration;

/// <summary>Observes and interleaves original production persistence while retaining actual provider CAS.</summary>
internal sealed class ReviewFiveLiveBatchStore(DaprClient client, DaprReadModelStore inner,
    Func<string, ReadOnlyMemory<byte>, CancellationToken, Task> beforeWrite) : IReadModelBatchStore, IReadModelPendingBatchRecoveryStore
{
    public Task<ReadModelBatchResult> ExecuteAsync(ReadModelBatch batch, CancellationToken token = default)
        => new ReadModelBatchProtocol(new DelayedScratchLiveAccessor(new DaprReadModelBatchStateAccessor(client, batch.Scope.StoreName), beforeWrite),
            new(), NullLogger.Instance).ExecuteAsync(batch, null, token);

    public Task<ReadModelPendingBatchRecoveryResult> RecoverPendingAsync(ReadModelBatchScope scope, string key, CancellationToken token = default)
        => inner.RecoverPendingAsync(scope, key, token);
}
