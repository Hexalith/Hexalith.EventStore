using Hexalith.EventStore.Client.Processes;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Testing.Fakes;

namespace Hexalith.EventStore.DomainService.Tests.Processes;

/// <summary>Faults a bounded number of durable source observation marker writes.</summary>
internal sealed class FaultOnObservationStore(InMemoryReadModelStore inner, ProcessSource? blockedSource, int failures) : IReadModelStore
{
    private int _remaining = failures;

    /// <inheritdoc/>
    public Task<ReadModelEntry<T>> GetAsync<T>(string storeName, string key, CancellationToken cancellationToken = default) where T : class
        => inner.GetAsync<T>(storeName, key, cancellationToken);

    /// <inheritdoc/>
    public Task SaveAsync<T>(string storeName, string key, T value, CancellationToken cancellationToken = default) where T : class
        => inner.SaveAsync(storeName, key, value, cancellationToken);

    /// <inheritdoc/>
    public Task<bool> TrySaveAsync<T>(string storeName, string key, T value, string etag, CancellationToken cancellationToken = default) where T : class
    {
        if (_remaining > 0 && value is ProcessSourceState { ObservedCycle: > 0 } state
            && (blockedSource is null || state.Source == blockedSource))
        {
            _remaining--;
            throw new InvalidOperationException("Synthetic crash before source observation marker write.");
        }

        return inner.TrySaveAsync(storeName, key, value, etag, cancellationToken);
    }
}
