using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Testing.Fakes;

namespace Hexalith.EventStore.DomainService.Tests.Processes;

/// <summary>Injects a single registry read admission failure before returning page evidence.</summary>
internal sealed class AdmissionFaultProcessStore(IReadModelStore inner, string failedKey, bool afterSourceRelease = false, int refusals = 1) : IReadModelStore
{
    private bool _armed = !afterSourceRelease;

    /// <summary>Whether the configured read failure fired.</summary>
    public bool Fired { get; private set; }

    /// <summary>Number of refused reads, including repeated passes.</summary>
    public int Refusals { get; private set; }

    /// <inheritdoc/>
    public Task<ReadModelEntry<T>> GetAsync<T>(string storeName, string key, CancellationToken cancellationToken = default) where T : class
    {
        if (key == failedKey && _armed && Refusals < refusals)
        {
            Fired = true;
            Refusals++;
            return Task.FromException<ReadModelEntry<T>>(new ProcessReadAdmissionException());
        }
        return inner.GetAsync<T>(storeName, key, cancellationToken);
    }

    /// <inheritdoc/>
    public Task SaveAsync<T>(string storeName, string key, T value, CancellationToken cancellationToken = default) where T : class
        => inner.SaveAsync(storeName, key, value, cancellationToken);

    /// <inheritdoc/>
    public async Task<bool> TrySaveAsync<T>(string storeName, string key, T value, string etag, CancellationToken cancellationToken = default) where T : class
    {
        bool saved = await inner.TrySaveAsync(storeName, key, value, etag, cancellationToken).ConfigureAwait(false);
        if (saved && value is ProcessSourceState { Checkpoint: > 0, Owner: null }) _armed = true;
        return saved;
    }
}
