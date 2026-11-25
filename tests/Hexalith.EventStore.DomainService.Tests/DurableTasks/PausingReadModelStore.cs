using Hexalith.EventStore.Client.Projections;

namespace Hexalith.EventStore.DomainService.Tests.DurableTasks;

/// <summary>Delegates to a store and can hold the next conditional write until the test releases it.</summary>
/// <param name="inner">The store that persists the values.</param>
internal sealed class PausingReadModelStore(IReadModelStore inner) : IReadModelStore
{
    private readonly Lock _gate = new();
    private TaskCompletionSource? _reached;
    private TaskCompletionSource? _release;

    /// <summary>Holds the next conditional write after its caller has read the row, before the write reaches the store.</summary>
    /// <returns>A task that completes when the write is held, and the action that releases it.</returns>
    public (Task Reached, Action Release) PauseNextConditionalWrite()
    {
        var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_gate)
        {
            _reached = reached;
            _release = release;
        }

        return (reached.Task, release.SetResult);
    }

    /// <inheritdoc/>
    public Task<ReadModelEntry<TValue>> GetAsync<TValue>(string storeName, string key, CancellationToken cancellationToken = default)
        where TValue : class
        => inner.GetAsync<TValue>(storeName, key, cancellationToken);

    /// <inheritdoc/>
    public Task SaveAsync<TValue>(string storeName, string key, TValue value, CancellationToken cancellationToken = default)
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
        TaskCompletionSource? reached;
        TaskCompletionSource? release;
        lock (_gate)
        {
            (reached, release) = (_reached, _release);
            (_reached, _release) = (null, null);
        }

        if (reached is not null && release is not null)
        {
            reached.SetResult();
            await release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        return await inner.TrySaveAsync(storeName, key, value, etag, cancellationToken).ConfigureAwait(false);
    }
}
