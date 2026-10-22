using Hexalith.EventStore.Client.Projections;

namespace Hexalith.EventStore.Client.Tests.Projections;

internal sealed class ReviewThreeBatchAccessor(IReadModelBatchStateAccessor inner) : IReadModelBatchStateAccessor
{
    internal Func<string, ReadOnlyMemory<byte>, CancellationToken, Task>? BeforeWrite { get; set; }
    internal Func<string, RawStateEntry, CancellationToken, Task>? AfterRead { get; set; }
    public bool SupportsTransaction => inner.SupportsTransaction;
    public async Task<RawStateEntry> ReadAsync(string key, CancellationToken token)
    {
        RawStateEntry row = await inner.ReadAsync(key, token);
        if (AfterRead is not null) await AfterRead(key, row, token);
        return row;
    }
    public Task<bool> TryWriteAsync(string key, ReadOnlyMemory<byte> value, string etag, CancellationToken token)
        => TryWriteAsync(key, value, etag, null, token);
    public async Task<bool> TryWriteAsync(string key, ReadOnlyMemory<byte> value, string etag, TimeSpan? ttl, CancellationToken token)
    {
        if (BeforeWrite is not null) await BeforeWrite(key, value, token);
        return await inner.TryWriteAsync(key, value, etag, ttl, token);
    }
    public Task<bool> TryDeleteAsync(string key, string etag, CancellationToken token) => inner.TryDeleteAsync(key, etag, token);
    public Task ExecuteTransactionAsync(IReadOnlyList<RawTransactionOperation> operations, CancellationToken token)
        => inner.ExecuteTransactionAsync(operations, token);
}
