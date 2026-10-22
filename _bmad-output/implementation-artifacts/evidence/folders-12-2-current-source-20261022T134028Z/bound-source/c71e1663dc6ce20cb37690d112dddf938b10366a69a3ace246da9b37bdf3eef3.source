using Hexalith.EventStore.Client.Projections;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Integration;

/// <summary>Injects a process stop at one Redis-backed conditional write.</summary>
internal sealed class FaultingSharedProjectionStore(
    IReadModelStore inner,
    int failOnWrite,
    bool failAfterWrite = false) : IReadModelStore
{
    private int _writes;

    /// <inheritdoc/>
    public Task<ReadModelEntry<TValue>> GetAsync<TValue>(
        string storeName,
        string key,
        CancellationToken cancellationToken = default)
        where TValue : class
        => inner.GetAsync<TValue>(storeName, key, cancellationToken);

    /// <inheritdoc/>
    public Task SaveAsync<TValue>(
        string storeName,
        string key,
        TValue value,
        CancellationToken cancellationToken = default)
        where TValue : class
        => inner.SaveAsync(storeName, key, value, cancellationToken);

    /// <inheritdoc/>
    public async Task<bool> TrySaveAsync<TValue>(
        string storeName,
        string key,
        TValue value,
        string etag,
        CancellationToken cancellationToken = default)
        where TValue : class
    {
        int write = Interlocked.Increment(ref _writes);
        if (write == failOnWrite && !failAfterWrite)
        {
            throw new InvalidOperationException("Injected host stop before conditional state write.");
        }

        bool saved = await inner.TrySaveAsync(storeName, key, value, etag, cancellationToken);
        if (write == failOnWrite && failAfterWrite && saved)
        {
            throw new InvalidOperationException("Injected host stop after conditional state write.");
        }

        return saved;
    }
}
