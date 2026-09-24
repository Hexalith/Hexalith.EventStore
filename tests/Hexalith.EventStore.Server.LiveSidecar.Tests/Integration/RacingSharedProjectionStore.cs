using Hexalith.EventStore.Client.Projections;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Integration;

/// <summary>Releases two epoch CAS writes together to exercise the selected provider's conflict.</summary>
internal sealed class RacingSharedProjectionStore(
    IReadModelStore inner,
    string epochStateKey,
    SemaphoreSlim arrived,
    SemaphoreSlim release) : IReadModelStore
{
    private int _intercepted;

    internal string? InterceptedEtag { get; private set; }

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
        if (string.Equals(key, epochStateKey, StringComparison.Ordinal)
            && Interlocked.CompareExchange(ref _intercepted, 1, 0) == 0)
        {
            InterceptedEtag = etag;
            arrived.Release();
            await release.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        return await inner.TrySaveAsync(storeName, key, value, etag, cancellationToken).ConfigureAwait(false);
    }
}
