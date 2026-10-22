using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.DomainService;

namespace Hexalith.EventStore.DomainService.Tests.Processes;

/// <summary>Refuses the sweep save that yields an already recorded read admission.</summary>
internal sealed class SweepYieldAdmissionFaultStore(IReadModelStore inner) : IReadModelStore
{
    private int _thrown;

    /// <summary>Whether the yielding sweep save was refused.</summary>
    public bool Fired => Volatile.Read(ref _thrown) == 1;

    /// <inheritdoc/>
    public Task<ReadModelEntry<T>> GetAsync<T>(string storeName, string key, CancellationToken cancellationToken = default) where T : class
        => inner.GetAsync<T>(storeName, key, cancellationToken);

    /// <inheritdoc/>
    public Task SaveAsync<T>(string storeName, string key, T value, CancellationToken cancellationToken = default) where T : class
        => inner.SaveAsync(storeName, key, value, cancellationToken);

    /// <inheritdoc/>
    public Task<bool> TrySaveAsync<T>(string storeName, string key, T value, string etag, CancellationToken cancellationToken = default) where T : class
    {
        if (value is ProcessSweepState { ScanReadAdmissionRecorded: true, NextCatalog: true }
            && Interlocked.Exchange(ref _thrown, 1) == 0)
            return Task.FromException<bool>(new ProcessReadAdmissionException());
        return inner.TrySaveAsync(storeName, key, value, etag, cancellationToken);
    }
}
