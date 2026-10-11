using Hexalith.EventStore.Client.Processes;

namespace Hexalith.EventStore.DomainService.Tests.Processes;

/// <summary>A scoped catalog that supports asynchronous disposal only.</summary>
internal sealed class AsyncDisposableCatalog : IProcessSourceCatalog, IAsyncDisposable
{
    private readonly AsyncCatalogDisposalTracker _tracker;

    /// <summary>Initializes a catalog and records its creation.</summary>
    /// <param name="tracker">Tracks creation and disposal.</param>
    public AsyncDisposableCatalog(AsyncCatalogDisposalTracker tracker)
    {
        _tracker = tracker;
        _tracker.RecordCreated();
    }

    /// <inheritdoc/>
    public Task<ProcessSourcePage> ListAsync(string? afterKey, int limit, CancellationToken cancellationToken = default)
        => Task.FromResult(new ProcessSourcePage([], null));

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        _tracker.RecordDisposed();
        return ValueTask.CompletedTask;
    }
}
