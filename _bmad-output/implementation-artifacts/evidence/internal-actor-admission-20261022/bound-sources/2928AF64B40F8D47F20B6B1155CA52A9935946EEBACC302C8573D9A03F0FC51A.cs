namespace Hexalith.EventStore.Client.Projections;

/// <summary>Counts actual owning-store recovery operations, including failed mutation and transaction attempts.</summary>
internal sealed class ReadModelBatchMeasuredStateAccessor(IReadModelBatchStateAccessor inner) : IReadModelBatchStateAccessor
{
    public bool SupportsTransaction => inner.SupportsTransaction;
    internal int ReadCount { get; private set; }
    internal long ReadBytes { get; private set; }
    internal long MutationAttempts { get; private set; }
    internal long MutationBytes { get; private set; }
    internal long RpcAttempts { get; private set; }

    public async Task<RawStateEntry> ReadAsync(string key, CancellationToken cancellationToken)
    {
        ReadCount++;
        RpcAttempts++;
        RawStateEntry entry = await inner.ReadAsync(key, cancellationToken).ConfigureAwait(false);
        ReadBytes += entry.Value.Length;
        return entry;
    }

    public Task<bool> TryWriteAsync(string key, ReadOnlyMemory<byte> value, string expectedETag, CancellationToken cancellationToken)
        => TryWriteAsync(key, value, expectedETag, null, cancellationToken);

    public async Task<bool> TryWriteAsync(string key, ReadOnlyMemory<byte> value, string expectedETag, TimeSpan? timeToLive,
        CancellationToken cancellationToken)
    {
        MutationAttempts++;
        MutationBytes += value.Length;
        RpcAttempts++;
        return await inner.TryWriteAsync(key, value, expectedETag, timeToLive, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> TryDeleteAsync(string key, string expectedETag, CancellationToken cancellationToken)
    {
        MutationAttempts++;
        RpcAttempts++;
        return await inner.TryDeleteAsync(key, expectedETag, cancellationToken).ConfigureAwait(false);
    }

    public async Task ExecuteTransactionAsync(IReadOnlyList<RawTransactionOperation> operations, CancellationToken cancellationToken)
    {
        MutationAttempts += operations.Count;
        MutationBytes += operations.Sum(static operation => (long)operation.Value.Length);
        RpcAttempts++;
        await inner.ExecuteTransactionAsync(operations, cancellationToken).ConfigureAwait(false);
    }
}
