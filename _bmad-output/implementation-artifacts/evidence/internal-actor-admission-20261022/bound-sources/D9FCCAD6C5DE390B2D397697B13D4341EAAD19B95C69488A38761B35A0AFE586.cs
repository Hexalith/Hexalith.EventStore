using Hexalith.EventStore.Client.Projections;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Integration;

/// <summary>Measures exact physical bytes while retaining the production Dapr byte-state adapter.</summary>
internal sealed class ProjectionBoundedIdentityLiveAccessor(IReadModelBatchStateAccessor inner,
    ProjectionBoundedIdentityLiveMeasurements measurements) : IReadModelBatchStateAccessor
{
    public bool SupportsTransaction => inner.SupportsTransaction;
    public Task<RawStateEntry> ReadAsync(string key, CancellationToken cancellationToken) => inner.ReadAsync(key, cancellationToken);
    public Task<bool> TryWriteAsync(string key, ReadOnlyMemory<byte> value, string expectedETag, CancellationToken cancellationToken)
    {
        measurements.ObserveValue(key, value.Length);
        return inner.TryWriteAsync(key, value, expectedETag, cancellationToken);
    }
    public Task<bool> TryWriteAsync(string key, ReadOnlyMemory<byte> value, string expectedETag, TimeSpan? timeToLive, CancellationToken cancellationToken)
    {
        measurements.ObserveValue(key, value.Length);
        return inner.TryWriteAsync(key, value, expectedETag, timeToLive, cancellationToken);
    }
    public Task<bool> TryDeleteAsync(string key, string expectedETag, CancellationToken cancellationToken) => inner.TryDeleteAsync(key, expectedETag, cancellationToken);
    public Task ExecuteTransactionAsync(IReadOnlyList<RawTransactionOperation> operations, CancellationToken cancellationToken)
    {
        foreach (var operation in operations) measurements.ObserveValue(operation.Key, operation.Value.Length);
        return inner.ExecuteTransactionAsync(operations, cancellationToken);
    }
}
