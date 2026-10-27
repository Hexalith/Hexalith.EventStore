using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Testing.Fakes;

namespace Hexalith.EventStore.DomainService.Tests.Processes;

/// <summary>Holds a renewal until the runner has released its completed source, then fails renewal.</summary>
internal sealed class SweepRenewalFaultStore(InMemoryReadModelStore inner) : IReadModelStore
{
    private readonly TaskCompletionSource _renewalEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _sourceReleased = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _renewalFaulted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _armed;

    /// <summary>The held renewal has reached its state write.</summary>
    public Task RenewalEntered => _renewalEntered.Task;

    /// <summary>The detached renewal worker has executed its synthetic fault.</summary>
    public Task RenewalFaulted => _renewalFaulted.Task;

    /// <summary>Whether the renewal fault fired.</summary>
    public bool Fired { get; private set; }

    /// <summary>Arms the renewal fault once translation starts.</summary>
    public void Arm() => Volatile.Write(ref _armed, 1);

    /// <summary>Releases held renewal work during failed-test cleanup.</summary>
    public void Release() => _sourceReleased.TrySetResult();

    /// <inheritdoc/>
    public Task<ReadModelEntry<T>> GetAsync<T>(string storeName, string key, CancellationToken cancellationToken = default) where T : class
        => inner.GetAsync<T>(storeName, key, cancellationToken);

    /// <inheritdoc/>
    public Task SaveAsync<T>(string storeName, string key, T value, CancellationToken cancellationToken = default) where T : class
        => inner.SaveAsync(storeName, key, value, cancellationToken);

    /// <inheritdoc/>
    public async Task<bool> TrySaveAsync<T>(string storeName, string key, T value, string etag, CancellationToken cancellationToken = default) where T : class
    {
        if (value is ProcessSweepState && Interlocked.CompareExchange(ref _armed, 2, 1) == 1)
        {
            _renewalEntered.TrySetResult();
            await _sourceReleased.Task.ConfigureAwait(false);
            Fired = true;
            _renewalFaulted.TrySetResult();
            throw new InvalidOperationException("Synthetic renewal failure after source completion.");
        }
        bool saved = await inner.TrySaveAsync(storeName, key, value, etag, cancellationToken).ConfigureAwait(false);
        if (saved && value is ProcessSourceState { Checkpoint: 2, Status: ProcessSourceStatus.Active, Owner: null })
        {
            Volatile.Write(ref _armed, 0);
            _sourceReleased.TrySetResult();
        }
        return saved;
    }
}
