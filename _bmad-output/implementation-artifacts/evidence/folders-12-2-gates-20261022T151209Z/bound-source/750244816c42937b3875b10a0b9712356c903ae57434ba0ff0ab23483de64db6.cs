using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Testing.Fakes;

namespace Hexalith.EventStore.DomainService.Tests.Processes;

/// <summary>Pauses one source-state CAS after sweep invalidation and before the source write.</summary>
internal sealed class PausingProcessSourceStore(InMemoryReadModelStore inner, string sourceKey) : IReadModelStore
{
    private readonly TaskCompletionSource _paused = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _armed = 1;

    /// <summary>Completes when the source write is paused.</summary>
    public Task Paused => _paused.Task;

    /// <summary>Allows the pending source write to reach durable storage.</summary>
    public void Release() => _release.TrySetResult();

    /// <inheritdoc/>
    public Task<ReadModelEntry<T>> GetAsync<T>(string storeName, string key, CancellationToken cancellationToken = default) where T : class
        => inner.GetAsync<T>(storeName, key, cancellationToken);

    /// <inheritdoc/>
    public Task SaveAsync<T>(string storeName, string key, T value, CancellationToken cancellationToken = default) where T : class
        => inner.SaveAsync(storeName, key, value, cancellationToken);

    /// <inheritdoc/>
    public async Task<bool> TrySaveAsync<T>(string storeName, string key, T value, string etag, CancellationToken cancellationToken = default) where T : class
    {
        if (key == sourceKey && value is ProcessSourceState { Status: ProcessSourceStatus.Incomplete }
            && Interlocked.Exchange(ref _armed, 0) == 1)
        {
            _paused.TrySetResult();
            await _release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        return await inner.TrySaveAsync(storeName, key, value, etag, cancellationToken).ConfigureAwait(false);
    }
}
