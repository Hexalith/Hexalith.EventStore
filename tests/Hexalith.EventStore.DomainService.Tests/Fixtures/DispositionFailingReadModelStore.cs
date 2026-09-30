using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Testing.Fakes;

namespace Hexalith.EventStore.DomainService.Tests.Fixtures;

/// <summary>Delegates to the durable fake store, but can fail every audit disposition write.</summary>
internal sealed class DispositionFailingReadModelStore(InMemoryReadModelStore inner) : IReadModelStore, IReadModelConditionalEraser
{
    /// <summary>Gets or sets a value indicating whether writes to <c>:disposition:</c> keys throw.</summary>
    public bool FailDispositionWrites { get; set; }

    /// <summary>Gets or sets a predicate that rejects selected conditional writes.</summary>
    public Func<string, bool>? RejectTrySave { get; set; }

    /// <inheritdoc/>
    public Task<ReadModelEntry<TValue>> GetAsync<TValue>(string storeName, string key, CancellationToken cancellationToken = default)
        where TValue : class
        => inner.GetAsync<TValue>(storeName, key, cancellationToken);

    /// <inheritdoc/>
    public Task SaveAsync<TValue>(string storeName, string key, TValue value, CancellationToken cancellationToken = default)
        where TValue : class
        => FailDispositionWrites && key.Contains(":disposition:", StringComparison.Ordinal)
            ? throw new InvalidOperationException("Synthetic audit store outage.")
            : inner.SaveAsync(storeName, key, value, cancellationToken);

    /// <inheritdoc/>
    public Task<bool> TrySaveAsync<TValue>(string storeName, string key, TValue value, string etag, CancellationToken cancellationToken = default)
        where TValue : class
        => RejectTrySave?.Invoke(key) == true
            ? Task.FromResult(false)
            : inner.TrySaveAsync(storeName, key, value, etag, cancellationToken);

    /// <inheritdoc/>
    public Task<bool> TryEraseAsync(string storeName, string key, string etag, CancellationToken cancellationToken = default)
        => inner.TryEraseAsync(storeName, key, etag, cancellationToken);

    /// <inheritdoc/>
    public Task<(bool Present, string Etag)> TryReadEtagAsync(string storeName, string key, CancellationToken cancellationToken = default)
        => inner.TryReadEtagAsync(storeName, key, cancellationToken);
}
