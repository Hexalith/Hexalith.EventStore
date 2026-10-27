using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Testing.Fakes;

namespace Hexalith.EventStore.Client.Tests.Projections;

/// <summary>Injects a bounded interruption or source mutation after a committed batch.</summary>
/// <param name="inner">The store retained across recreation.</param>
internal sealed class AbortRepairBatchFaultStore(InMemoryReadModelStore inner) : IReadModelBatchStore
{
    /// <summary>Gets or sets whether the next metadata delete is interrupted after commit.</summary>
    public bool FailRepair { get; set; }

    /// <summary>Gets or sets an observer invoked after durable batch execution.</summary>
    public Action<ReadModelBatch>? AfterExecute { get; set; }

    /// <inheritdoc />
    public async Task<ReadModelBatchResult> ExecuteAsync(ReadModelBatch batch, CancellationToken cancellationToken = default)
    {
        ReadModelBatchResult result = await inner.ExecuteAsync(batch, cancellationToken);
        AfterExecute?.Invoke(batch);
        if (FailRepair && batch.Operations.Any(operation => operation.Kind == ReadModelBatchOperationKind.Delete))
        {
            FailRepair = false;
            throw new IOException("Interrupted after committed metadata delete");
        }
        return result;
    }
}
