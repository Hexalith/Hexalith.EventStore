using Hexalith.EventStore.Client.Projections;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Integration;

/// <summary>Pauses an actual awaited write without replacing provider CAS or persistence.</summary>
internal sealed class DelayedScratchLiveAccessor(IReadModelBatchStateAccessor inner,
    Func<string, ReadOnlyMemory<byte>, CancellationToken, Task> beforeWrite) : IReadModelBatchStateAccessor
{
    public bool SupportsTransaction => inner.SupportsTransaction;
    public Task<RawStateEntry> ReadAsync(string key, CancellationToken token) => inner.ReadAsync(key, token);
    public Task<bool> TryWriteAsync(string key, ReadOnlyMemory<byte> value, string etag, CancellationToken token)
        => TryWriteAsync(key, value, etag, null, token);
    public async Task<bool> TryWriteAsync(string key, ReadOnlyMemory<byte> value, string etag, TimeSpan? ttl, CancellationToken token)
    {
        await beforeWrite(key, value, token);
        return await inner.TryWriteAsync(key, value, etag, ttl, token);
    }
    public Task<bool> TryDeleteAsync(string key, string etag, CancellationToken token) => inner.TryDeleteAsync(key, etag, token);
    public Task ExecuteTransactionAsync(IReadOnlyList<RawTransactionOperation> operations, CancellationToken token)
        => inner.ExecuteTransactionAsync(operations, token);
}
