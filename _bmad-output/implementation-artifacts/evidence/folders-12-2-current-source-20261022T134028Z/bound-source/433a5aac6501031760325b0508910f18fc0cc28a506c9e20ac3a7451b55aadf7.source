using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Testing.Fakes;

namespace Hexalith.EventStore.DomainService.Tests.Processes;

/// <summary>Rejects one marker CAS, optionally after a competing durable write.</summary>
internal sealed class RejectProcessMarkerStore(InMemoryReadModelStore inner, string markerKey, bool persistBeforeReject = false, bool alwaysReject = false) : IReadModelStore
{
    private bool _armed = true;

    /// <inheritdoc/>
    public Task<ReadModelEntry<T>> GetAsync<T>(string storeName, string key, CancellationToken cancellationToken = default) where T : class
        => inner.GetAsync<T>(storeName, key, cancellationToken);

    /// <inheritdoc/>
    public Task SaveAsync<T>(string storeName, string key, T value, CancellationToken cancellationToken = default) where T : class
        => inner.SaveAsync(storeName, key, value, cancellationToken);

    /// <inheritdoc/>
    public async Task<bool> TrySaveAsync<T>(string storeName, string key, T value, string etag, CancellationToken cancellationToken = default) where T : class
    {
        if ((_armed || alwaysReject) && key == markerKey)
        {
            _armed = false;
            if (persistBeforeReject)
                _ = await inner.TrySaveAsync(storeName, key, value, etag, cancellationToken).ConfigureAwait(false);
            return false;
        }

        return await inner.TrySaveAsync(storeName, key, value, etag, cancellationToken).ConfigureAwait(false);
    }
}
