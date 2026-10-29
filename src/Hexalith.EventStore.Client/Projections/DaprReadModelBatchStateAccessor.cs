using Dapr.Client;

using Hexalith.EventStore.Contracts.Routing;

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
        ValidateKey(key, cancellationToken);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.MetadataReads);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.StateRpcAttempts);
        (ReadOnlyMemory<byte> value, string etag) = await RouteCatalogRemoteExecution.InvokeAsync(() => daprClient
            .GetByteStateAndETagAsync(storeName, key, cancellationToken: cancellationToken)).ConfigureAwait(false);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.MetadataReadBytes, value.Length);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.ModelBytes, value.Length);
        return string.IsNullOrEmpty(etag)
            ? new RawStateEntry(false, ReadOnlyMemory<byte>.Empty, string.Empty)
            : new RawStateEntry(true, value, etag);
    }

    /// <inheritdoc/>
    public async Task<bool> TryWriteAsync(string key, ReadOnlyMemory<byte> value, string expectedETag, CancellationToken cancellationToken) =>
        await TryWriteAsync(key, value, expectedETag, timeToLive: null, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc/>
    public async Task<bool> TryWriteAsync(
        string key,
        ReadOnlyMemory<byte> value,
        string expectedETag,
        TimeSpan? timeToLive,
        CancellationToken cancellationToken) {
        ValidateKey(key, cancellationToken);
        ArgumentNullException.ThrowIfNull(expectedETag);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.MutationAttempts);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.MutationBytes, value.Length);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.ModelBytes, value.Length);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.StateRpcAttempts);
        IReadOnlyDictionary<string, string>? metadata = timeToLive is null ? null
            : new Dictionary<string, string>(StringComparer.Ordinal) { ["ttlInSeconds"] = ToTtlSeconds(timeToLive.Value) };
        try {
            return await RouteCatalogRemoteExecution.InvokeAsync(() => daprClient
                .TrySaveByteStateAsync(
                    storeName,
                    key,
                    value,
                    expectedETag,
                    new StateOptions { Concurrency = ConcurrencyMode.FirstWrite },
                    metadata: metadata!,
                    cancellationToken: cancellationToken)).ConfigureAwait(false);
        }
        catch (Dapr.DaprException) when (expectedETag.Length == 0 && !cancellationToken.IsCancellationRequested) {
            if (await DaprReadModelCreateConflict.ExistsAsync(daprClient, storeName, key, cancellationToken).ConfigureAwait(false))
                return false;
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> TryDeleteAsync(string key, string expectedETag, CancellationToken cancellationToken) {
        ValidateKey(key, cancellationToken);
        ArgumentNullException.ThrowIfNull(expectedETag);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.MutationAttempts);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.StateRpcAttempts);
        return await RouteCatalogRemoteExecution.InvokeAsync(() => daprClient
            .TryDeleteStateAsync(
                storeName,
                key,
                expectedETag,
                new StateOptions { Concurrency = ConcurrencyMode.FirstWrite },
                cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task ExecuteTransactionAsync(IReadOnlyList<RawTransactionOperation> operations, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(operations);
        ArgumentException.ThrowIfNullOrWhiteSpace(storeName);
        cancellationToken.ThrowIfCancellationRequested();
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.TransactionAttempts);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.StateRpcAttempts);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.MutationAttempts, operations.Count);
        long bytes = operations.Sum(static operation => (long)operation.Value.Length);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.MutationBytes, bytes);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.ModelBytes, bytes);
        var requests = new List<StateTransactionRequest>(operations.Count);
        foreach (RawTransactionOperation operation in operations) {
            ArgumentException.ThrowIfNullOrWhiteSpace(operation.Key);
            requests.Add(new StateTransactionRequest(
                operation.Key,
                operation.IsDelete ? [] : operation.Value.ToArray(),
                operation.IsDelete ? StateOperationType.Delete : StateOperationType.Upsert,
                operation.FirstWrite ? operation.ETag : null,
                metadata: operation.TimeToLive is null
                    ? null
                    : new Dictionary<string, string>(StringComparer.Ordinal) {
                        ["ttlInSeconds"] = ToTtlSeconds(operation.TimeToLive.Value),
                    },
                options: operation.FirstWrite ? new StateOptions { Concurrency = ConcurrencyMode.FirstWrite } : null));
        }

        await RouteCatalogRemoteExecution.InvokeAsync(() => daprClient
            .ExecuteStateTransactionAsync(storeName, requests, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    private void ValidateKey(string key, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storeName);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static string ToTtlSeconds(TimeSpan timeToLive) {
        if (timeToLive <= TimeSpan.Zero) {
            throw new ArgumentOutOfRangeException(nameof(timeToLive), "Time-to-live must be greater than zero.");
        }

        return checked((long)Math.Ceiling(timeToLive.TotalSeconds))
            .ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
