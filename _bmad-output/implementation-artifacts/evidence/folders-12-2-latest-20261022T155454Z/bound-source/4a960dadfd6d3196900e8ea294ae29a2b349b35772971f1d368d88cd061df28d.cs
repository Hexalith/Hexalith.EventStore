using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Testing.Fakes;

namespace Hexalith.EventStore.DomainService.Tests.Processes;

/// <summary>Fails reads of one process discovery tail while retaining all other persisted state.</summary>
internal sealed class FaultOnProcessTailReadStore(InMemoryReadModelStore inner, string tailKey) : IReadModelStore
{
    /// <summary>Gets the number of faulted tail reads.</summary>
    public int TailReads { get; private set; }

    /// <inheritdoc/>
    public Task<ReadModelEntry<T>> GetAsync<T>(string storeName, string key, CancellationToken cancellationToken = default) where T : class
    {
        if (key == tailKey)
        {
            TailReads++;
            return Task.FromException<ReadModelEntry<T>>(new TimeoutException("Synthetic process tail-read timeout."));
        }

        return inner.GetAsync<T>(storeName, key, cancellationToken);
    }

    /// <inheritdoc/>
    public Task SaveAsync<T>(string storeName, string key, T value, CancellationToken cancellationToken = default) where T : class
        => inner.SaveAsync(storeName, key, value, cancellationToken);

    /// <inheritdoc/>
    public Task<bool> TrySaveAsync<T>(string storeName, string key, T value, string etag, CancellationToken cancellationToken = default) where T : class
        => inner.TrySaveAsync(storeName, key, value, etag, cancellationToken);
}
