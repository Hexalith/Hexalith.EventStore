using Hexalith.EventStore.Client.Projections;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Integration;

/// <summary>Retains real provider CAS while an original installer pauses and exits after its physical write.</summary>
internal sealed class ReviewFourLateInstallerAccessor(IReadModelBatchStateAccessor inner, string target,
    Func<CancellationToken, Task> pause) : IReadModelBatchStateAccessor
{
    private int _stopped;
    public bool SupportsTransaction => inner.SupportsTransaction;
    public Task<RawStateEntry> ReadAsync(string key, CancellationToken token) => inner.ReadAsync(key, token);
    public Task<bool> TryWriteAsync(string key, ReadOnlyMemory<byte> value, string etag, CancellationToken token)
        => TryWriteAsync(key, value, etag, null, token);
    public async Task<bool> TryWriteAsync(string key, ReadOnlyMemory<byte> value, string etag, TimeSpan? ttl, CancellationToken token)
    {
        bool delayed = key == target && ReadModelBatchEnvelope.IsEnvelope(value.Span) && Interlocked.Increment(ref _stopped) == 1;
        if (delayed) await pause(token);
        bool saved = await inner.TryWriteAsync(key, value, etag, ttl, token);
        if (delayed) throw new IOException("Original installer exits before post-write decision check; persisted=" + saved);
        return saved;
    }
    public Task<bool> TryDeleteAsync(string key, string etag, CancellationToken token) => inner.TryDeleteAsync(key, etag, token);
    public Task ExecuteTransactionAsync(IReadOnlyList<RawTransactionOperation> operations, CancellationToken token)
        => inner.ExecuteTransactionAsync(operations, token);
}
