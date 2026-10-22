using Hexalith.EventStore.Client.Projections;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Integration;

/// <summary>Interrupts the production compensator before or after its real conditional private delete.</summary>
internal sealed class InterruptedLegacyScratchLiveAccessor(IReadModelBatchStateAccessor inner, string scratch,
    bool afterDelete) : IReadModelBatchStateAccessor
{
    public bool SupportsTransaction => inner.SupportsTransaction;
    public Task<RawStateEntry> ReadAsync(string key, CancellationToken token) => inner.ReadAsync(key, token);
    public Task<bool> TryWriteAsync(string key, ReadOnlyMemory<byte> value, string etag, CancellationToken token)
        => inner.TryWriteAsync(key, value, etag, token);
    public Task<bool> TryWriteAsync(string key, ReadOnlyMemory<byte> value, string etag, TimeSpan? retention, CancellationToken token)
        => inner.TryWriteAsync(key, value, etag, retention, token);
    public async Task<bool> TryDeleteAsync(string key, string etag, CancellationToken token)
    {
        if (key == scratch && !afterDelete) throw new IOException("legacy compensation stopped before private delete");
        bool deleted = await inner.TryDeleteAsync(key, etag, token);
        if (key == scratch) throw new IOException("legacy compensation lost private delete response");
        return deleted;
    }
    public Task ExecuteTransactionAsync(IReadOnlyList<RawTransactionOperation> operations, CancellationToken token)
        => inner.ExecuteTransactionAsync(operations, token);
}
