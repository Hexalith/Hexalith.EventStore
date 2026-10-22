using Hexalith.EventStore.Client.Projections;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Integration;

/// <summary>Exercises a real provider conditional-delete refusal without changing persisted participant bytes.</summary>
internal sealed class RefusedExpiryDeleteLiveAccessor(IReadModelBatchStateAccessor inner, string target) : IReadModelBatchStateAccessor
{
    public bool SupportsTransaction => inner.SupportsTransaction;
    public Task<RawStateEntry> ReadAsync(string key, CancellationToken token) => inner.ReadAsync(key, token);
    public Task<bool> TryWriteAsync(string key, ReadOnlyMemory<byte> value, string etag, CancellationToken token)
        => inner.TryWriteAsync(key, value, etag, token);
    public Task<bool> TryWriteAsync(string key, ReadOnlyMemory<byte> value, string etag, TimeSpan? ttl, CancellationToken token)
        => inner.TryWriteAsync(key, value, etag, ttl, token);
    public Task<bool> TryDeleteAsync(string key, string etag, CancellationToken token)
        => inner.TryDeleteAsync(key, key == target ? "9223372036854775806" : etag, token);
    public Task ExecuteTransactionAsync(IReadOnlyList<RawTransactionOperation> operations, CancellationToken token)
        => inner.ExecuteTransactionAsync(operations, token);
}
