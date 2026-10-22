using Dapr.Client;
using Hexalith.EventStore.Client.Projections;
using Microsoft.Extensions.Logging.Abstractions;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Integration;

/// <summary>Runs the production protocol over the fixture sidecar with one explicitly armed process interruption.</summary>
internal sealed class ScratchRecoveryLiveBatchStore(DaprClient client, DaprReadModelStore store,
    ReadModelBatchPhase phase, int ordinal, string batchPrefix = "scratch") : IReadModelBatchStore, IReadModelPendingBatchRecoveryStore
{
    internal bool Armed { get; set; } = true;

    public Task<ReadModelBatchResult> ExecuteAsync(ReadModelBatch batch, CancellationToken cancellationToken = default)
        => Armed && batch.Scope.BatchId.StartsWith(batchPrefix, StringComparison.Ordinal)
            ? new ReadModelBatchProtocol(new DaprReadModelBatchStateAccessor(client, batch.Scope.StoreName),
                new ReadModelBatchOptions(), NullLogger.Instance).ExecuteAsync(batch,
                    new RetainedBatchLiveFaultInjector(phase, ordinal), cancellationToken)
            : store.ExecuteAsync(batch, cancellationToken);

    public Task<ReadModelPendingBatchRecoveryResult> RecoverPendingAsync(ReadModelBatchScope scope, string key,
        CancellationToken cancellationToken = default) => store.RecoverPendingAsync(scope, key, cancellationToken);
}
