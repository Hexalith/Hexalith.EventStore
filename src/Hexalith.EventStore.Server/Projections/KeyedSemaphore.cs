using System.Collections.Concurrent;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>
/// Per-key async mutex that disposes its <see cref="SemaphoreSlim"/> entries when the last
/// holder releases. Prevents the unbounded-growth shape of a static
/// <see cref="ConcurrentDictionary{TKey, TValue}"/> of <see cref="SemaphoreSlim"/> in a
/// long-running multi-tenant server.
/// </summary>
internal sealed class KeyedSemaphore<TKey> where TKey : notnull {
    private readonly ConcurrentDictionary<TKey, Holder> _holders;

    public KeyedSemaphore(IEqualityComparer<TKey>? comparer = null) =>
        _holders = new ConcurrentDictionary<TKey, Holder>(comparer ?? EqualityComparer<TKey>.Default);

    /// <summary>
    /// Number of live keyed entries. Exposed for regression testing of the eviction contract.
    /// </summary>
    public int Count => _holders.Count;

    /// <summary>
    /// Acquires the per-key mutex. The returned <see cref="IDisposable"/> must be disposed to
    /// release. Disposing the last holder evicts the entry and disposes the underlying semaphore.
    /// </summary>
    public async Task<IDisposable> AcquireAsync(TKey key, CancellationToken cancellationToken) {
        Holder holder = AcquireRef(key);
        try {
            await holder.Semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch {
            ReleaseRef(key, holder);
            throw;
        }

        return new Releaser(this, key, holder);
    }

    private Holder AcquireRef(TKey key) {
        SpinWait spin = default;
        while (true) {
            Holder holder = _holders.GetOrAdd(key, static _ => new Holder());
            int newCount = Interlocked.Increment(ref holder.RefCount);
            if (newCount > 0) {
                return holder;
            }

            // Holder is being torn down by a concurrent ReleaseRef (RefCount poisoned with
            // int.MinValue). Yield briefly, then retry — the disposing caller will TryRemove
            // imminently and the next GetOrAdd will create a fresh holder.
            spin.SpinOnce();
        }
    }

    private void ReleaseRef(TKey key, Holder holder) {
        if (Interlocked.Decrement(ref holder.RefCount) != 0) {
            return;
        }

        // Claim disposal atomically: 0 -> int.MinValue. If a concurrent AcquireRef bumped the
        // count back up before we got here, CAS fails and we leave the holder for that caller.
        if (Interlocked.CompareExchange(ref holder.RefCount, int.MinValue, 0) != 0) {
            return;
        }

        _ = _holders.TryRemove(KeyValuePair.Create(key, holder));
        holder.Semaphore.Dispose();
    }

    private sealed class Holder {
        public readonly SemaphoreSlim Semaphore = new(1, 1);
        public int RefCount;
    }

    private sealed class Releaser(KeyedSemaphore<TKey> owner, TKey key, Holder holder) : IDisposable {
        private int _disposed;

        public void Dispose() {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) {
                return;
            }

            _ = holder.Semaphore.Release();
            owner.ReleaseRef(key, holder);
        }
    }
}
