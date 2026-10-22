using Dapr.Client;
using Hexalith.EventStore.Client.Projections;
using Microsoft.Extensions.Logging.Abstractions;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Integration;

/// <summary>Records an actual original SDK envelope while the production named dispatcher executes normally.</summary>
internal sealed class CapturingScratchLiveBatchStore(DaprClient client, DaprReadModelStore inner, string target)
    : IReadModelBatchStore, IReadModelPendingBatchRecoveryStore
{
    internal ReadOnlyMemory<byte> OriginalEnvelope { get; private set; }
    public Task<ReadModelBatchResult> ExecuteAsync(ReadModelBatch batch, CancellationToken token = default)
    {
        var accessor = new DelayedScratchLiveAccessor(new DaprReadModelBatchStateAccessor(client, batch.Scope.StoreName), (key, bytes, _) =>
        {
            if (key == target && ReadModelBatchEnvelope.IsEnvelope(bytes.Span) && OriginalEnvelope.IsEmpty) OriginalEnvelope = bytes.ToArray();
            return Task.CompletedTask;
        });
        return new ReadModelBatchProtocol(accessor, new(), NullLogger.Instance).ExecuteAsync(batch, null, token);
    }
    public Task<ReadModelPendingBatchRecoveryResult> RecoverPendingAsync(ReadModelBatchScope scope, string key, CancellationToken token = default)
        => inner.RecoverPendingAsync(scope, key, token);
}
