using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Testing.Fakes;

namespace Hexalith.EventStore.Client.Tests.Projections;

/// <summary>Injects bounded interruptions after durable repair or journal state writes.</summary>
/// <param name="inner">The store retained across recreation.</param>
internal sealed class AbortRepairFaultStore(InMemoryReadModelStore inner) : IReadModelStore
{
    /// <summary>Gets or sets whether the external repair evidence write fails.</summary>
    public bool FailEvidence { get; set; }

    /// <summary>Gets or sets the predicate selecting a committed state write to interrupt.</summary>
    public Func<object, bool>? InterruptAfterSave { get; set; }

    /// <inheritdoc />
    public Task<ReadModelEntry<T>> GetAsync<T>(string storeName, string key, CancellationToken cancellationToken = default) where T : class
        => inner.GetAsync<T>(storeName, key, cancellationToken);

    /// <inheritdoc />
    public Task SaveAsync<T>(string storeName, string key, T value, CancellationToken cancellationToken = default) where T : class
        => inner.SaveAsync(storeName, key, value, cancellationToken);

    /// <inheritdoc />
    public async Task<bool> TrySaveAsync<T>(string storeName, string key, T value, string etag, CancellationToken cancellationToken = default) where T : class
    {
        if (FailEvidence && key.Contains(":abort-repair:", StringComparison.Ordinal))
        {
            throw new IOException("Interrupted repair evidence write");
        }
        bool saved = await inner.TrySaveAsync(storeName, key, value, etag, cancellationToken);
        if (saved && InterruptAfterSave?.Invoke(value) == true)
        {
            throw new IOException("Interrupted after durable state write");
        }
        return saved;
    }
}
