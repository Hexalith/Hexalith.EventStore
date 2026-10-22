using Hexalith.EventStore.Client.Projections;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Integration;

/// <summary>Retains the actual provider transaction rejection alongside unchanged SDK read-back evidence.</summary>
internal sealed class ObservedTransactionLiveAccessor(IReadModelBatchStateAccessor inner, Action<Exception> observe) : IReadModelBatchStateAccessor
{
    public bool SupportsTransaction => inner.SupportsTransaction;
    public Task<RawStateEntry> ReadAsync(string key, CancellationToken token) => inner.ReadAsync(key, token);
    public Task<bool> TryWriteAsync(string key, ReadOnlyMemory<byte> value, string etag, CancellationToken token)
        => inner.TryWriteAsync(key, value, etag, token);
    public Task<bool> TryWriteAsync(string key, ReadOnlyMemory<byte> value, string etag, TimeSpan? retention, CancellationToken token)
        => inner.TryWriteAsync(key, value, etag, retention, token);
    public Task<bool> TryDeleteAsync(string key, string etag, CancellationToken token) => inner.TryDeleteAsync(key, etag, token);
    public async Task ExecuteTransactionAsync(IReadOnlyList<RawTransactionOperation> operations, CancellationToken token)
    {
        try { await inner.ExecuteTransactionAsync(operations, token); }
        catch (Exception exception) { observe(exception); throw; }
    }
}
