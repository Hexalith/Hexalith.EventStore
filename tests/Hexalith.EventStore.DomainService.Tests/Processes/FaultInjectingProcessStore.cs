using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Testing.Fakes;

namespace Hexalith.EventStore.DomainService.Tests.Processes;

/// <summary>Simulates a crash after one successful durable process-control write.</summary>
internal sealed class FaultInjectingProcessStore(InMemoryReadModelStore inner, Func<string, object, bool> fault) : IReadModelStore
{
    private bool _armed = true;

    /// <summary>Gets the number of candidate-page reads, excluding tail hints.</summary>
    public int CandidatePageReads { get; private set; }

    /// <summary>Gets the number of tenant-page reads, excluding the tail hint.</summary>
    public int TenantPageReads { get; private set; }

    /// <inheritdoc/>
    public Task<ReadModelEntry<T>> GetAsync<T>(string storeName, string key, CancellationToken cancellationToken = default) where T : class
    {
        if (key.Contains(":candidates:", StringComparison.Ordinal) && !key.EndsWith(":tail", StringComparison.Ordinal)) CandidatePageReads++;
        if (key.Contains(":control:tenants:", StringComparison.Ordinal) && !key.EndsWith(":tail", StringComparison.Ordinal)) TenantPageReads++;
        return inner.GetAsync<T>(storeName, key, cancellationToken);
    }

    /// <inheritdoc/>
    public Task SaveAsync<T>(string storeName, string key, T value, CancellationToken cancellationToken = default) where T : class
        => inner.SaveAsync(storeName, key, value, cancellationToken);

    /// <inheritdoc/>
    public async Task<bool> TrySaveAsync<T>(string storeName, string key, T value, string etag, CancellationToken cancellationToken = default) where T : class
    {
        bool saved = await inner.TrySaveAsync(storeName, key, value, etag, cancellationToken).ConfigureAwait(false);
        if (saved && _armed && fault(key, value))
        {
            _armed = false;
            throw new InvalidOperationException("Synthetic crash after durable write.");
        }

        return saved;
    }
}
