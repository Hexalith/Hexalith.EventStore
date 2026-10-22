using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Testing.Fakes;

namespace Hexalith.EventStore.DomainService.Tests.Processes;

/// <summary>Fails one discovery page read while retaining the persisted index and other pages.</summary>
internal sealed class FaultOnProcessPageReadStore(InMemoryReadModelStore inner, string pageKey, bool missing = false) : IReadModelStore
{
    /// <summary>Gets the number of failed reads.</summary>
    public int PageReads { get; private set; }

    /// <inheritdoc/>
    public Task<ReadModelEntry<T>> GetAsync<T>(string storeName, string key, CancellationToken cancellationToken = default) where T : class
    {
        if (key == pageKey)
        {
            PageReads++;
            return missing ? Task.FromResult(new ReadModelEntry<T>(null, null))
                : Task.FromException<ReadModelEntry<T>>(new TimeoutException("Synthetic process page-read timeout."));
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
