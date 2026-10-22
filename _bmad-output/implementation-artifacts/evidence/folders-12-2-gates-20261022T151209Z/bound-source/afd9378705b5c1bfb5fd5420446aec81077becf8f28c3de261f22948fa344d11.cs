using Hexalith.EventStore.Client.Processes;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Testing.Fakes;

namespace Hexalith.EventStore.DomainService.Tests.Processes;

/// <summary>Fails one exact marker write after its candidate page has become durable.</summary>
internal sealed class FaultOnceOnCandidateMarkerStore(InMemoryReadModelStore inner, ProcessSource source) : IReadModelStore
{
    private bool _fault = true;

    /// <inheritdoc/>
    public Task<ReadModelEntry<T>> GetAsync<T>(string storeName, string key, CancellationToken cancellationToken = default) where T : class
        => inner.GetAsync<T>(storeName, key, cancellationToken);

    /// <inheritdoc/>
    public Task SaveAsync<T>(string storeName, string key, T value, CancellationToken cancellationToken = default) where T : class
        => inner.SaveAsync(storeName, key, value, cancellationToken);

    /// <inheritdoc/>
    public Task<bool> TrySaveAsync<T>(string storeName, string key, T value, string etag, CancellationToken cancellationToken = default) where T : class
    {
        if (_fault && key == ProcessStateKeys.CandidateMarker(source))
        {
            _fault = false;
            throw new InvalidOperationException("Synthetic crash before marker publication.");
        }

        return inner.TrySaveAsync(storeName, key, value, etag, cancellationToken);
    }
}
