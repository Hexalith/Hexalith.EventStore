using Dapr.Client;

namespace Hexalith.EventStore.Client.Projections;

/// <summary>
/// DAPR-backed raw byte-state accessor for one store component, over which the coordinated batch protocol
/// runs. Reads and reconciliation use the pinned <see cref="DaprClient.GetByteStateAndETagAsync(string, string, ConsistencyMode?, IReadOnlyDictionary{string, string}, CancellationToken)"/>
/// so both legacy raw values and versioned batch envelopes can be decoded without changing existing typed
/// single-key behavior.
/// </summary>
/// <param name="daprClient">The DAPR client.</param>
/// <param name="storeName">The DAPR state-store component name.</param>
internal sealed class DaprReadModelBatchStateAccessor(DaprClient daprClient, string storeName)
    : IReadModelBatchStateAccessor {
    /// <inheritdoc/>
    public bool SupportsTransaction => true;

    /// <inheritdoc/>
    public async Task<RawStateEntry> ReadAsync(string key, CancellationToken cancellationToken) {
        (ReadOnlyMemory<byte> value, string etag) = await daprClient
            .GetByteStateAndETagAsync(storeName, key, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return string.IsNullOrEmpty(etag)
            ? new RawStateEntry(false, ReadOnlyMemory<byte>.Empty, string.Empty)
            : new RawStateEntry(true, value, etag);
    }

    /// <inheritdoc/>
    public async Task<bool> TryWriteAsync(string key, ReadOnlyMemory<byte> value, string expectedETag, CancellationToken cancellationToken) =>
        await daprClient
            .TrySaveByteStateAsync(
                storeName,
                key,
                value,
                expectedETag,
                new StateOptions { Concurrency = ConcurrencyMode.FirstWrite },
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc/>
    public async Task<bool> TryDeleteAsync(string key, string expectedETag, CancellationToken cancellationToken) =>
        await daprClient
            .TryDeleteStateAsync(
                storeName,
                key,
                expectedETag,
                new StateOptions { Concurrency = ConcurrencyMode.FirstWrite },
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc/>
    public async Task ExecuteTransactionAsync(IReadOnlyList<RawTransactionOperation> operations, CancellationToken cancellationToken) {
        var requests = new List<StateTransactionRequest>(operations.Count);
        foreach (RawTransactionOperation operation in operations) {
            requests.Add(new StateTransactionRequest(
                operation.Key,
                operation.IsDelete ? [] : operation.Value.ToArray(),
                operation.IsDelete ? StateOperationType.Delete : StateOperationType.Upsert,
                operation.FirstWrite ? operation.ETag : null,
                metadata: null,
                options: operation.FirstWrite ? new StateOptions { Concurrency = ConcurrencyMode.FirstWrite } : null));
        }

        await daprClient
            .ExecuteStateTransactionAsync(storeName, requests, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }
}
