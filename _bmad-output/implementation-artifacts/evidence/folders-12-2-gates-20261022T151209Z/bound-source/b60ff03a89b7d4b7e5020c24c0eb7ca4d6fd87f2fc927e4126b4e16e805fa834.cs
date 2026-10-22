using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Testing.Fakes;

namespace Hexalith.EventStore.DomainService.Tests.Processes;

/// <summary>Fails the first durable sweep read before hosted pass acquisition.</summary>
internal sealed class FaultOnceOnSweepReadStore(InMemoryReadModelStore inner) : IReadModelStore
{
    private int _remaining = 1;

    /// <inheritdoc/>
    public Task<ReadModelEntry<T>> GetAsync<T>(string storeName, string key, CancellationToken cancellationToken = default) where T : class
        => key == ProcessStateKeys.Sweep && Interlocked.Exchange(ref _remaining, 0) == 1
            ? Task.FromException<ReadModelEntry<T>>(new TimeoutException("Synthetic sweep-read failure."))
            : inner.GetAsync<T>(storeName, key, cancellationToken);

    /// <inheritdoc/>
    public Task SaveAsync<T>(string storeName, string key, T value, CancellationToken cancellationToken = default) where T : class
        => inner.SaveAsync(storeName, key, value, cancellationToken);

    /// <inheritdoc/>
    public Task<bool> TrySaveAsync<T>(string storeName, string key, T value, string etag, CancellationToken cancellationToken = default) where T : class
        => inner.TrySaveAsync(storeName, key, value, etag, cancellationToken);
}
