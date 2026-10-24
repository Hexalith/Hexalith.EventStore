// Effective authority: 08bfd5855998a276999db38447c409b2231f3d4e253d4e371d4d8bfe369462d6; base sections 10.5-10.7 and 15; amendment section 3.
using System.Security.Cryptography;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>Bounds plaintext keys, coalesces unwraps, and owns all cached arrays. Admission requires a current external lease.</summary>
internal sealed class PayloadKeyCache : IAsyncDisposable
{
    private readonly object _gate = new();
    private readonly Dictionary<PayloadKeyCacheKey, PayloadKeyCacheEntry> _entries = [];
    private readonly Dictionary<PayloadKeyCacheKey, TaskCompletionSource<UnreadableProtectedDataReason?>> _flights = [];
    private readonly Dictionary<string, int> _active = new(StringComparer.Ordinal);
    private readonly HashSet<string> _blocked = new(StringComparer.Ordinal);
    private readonly HashSet<string> _blockedRoots = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _scopeRoots = new(StringComparer.Ordinal);
    private readonly List<TaskCompletionSource> _drainWaiters = [];
    private readonly CancellationTokenSource _shutdown = new();
    private readonly int _capacity;
    private readonly TimeSpan _ttl;
    private readonly TimeProvider _clock;
    private readonly ISensitiveBufferObserver? _observer;
    private ulong _ordinal;
    private bool _disposed;

    /// <summary>Validates the absolute TTL and hard entry cap before any key can be stored.</summary>
    internal PayloadKeyCache(int capacity = 1024, TimeSpan? ttl = null, TimeProvider? timeProvider = null,
        ISensitiveBufferObserver? observer = null)
    {
        _capacity = capacity;
        _ttl = ttl ?? TimeSpan.FromSeconds(60);
        _clock = timeProvider ?? TimeProvider.System;
        _observer = observer;
        if (capacity is < 1 or > 4096 || _ttl <= TimeSpan.Zero || _ttl > TimeSpan.FromMinutes(5)) { throw new PayloadProtectionFormatException(); }
    }

    /// <summary>Returns an operation-owned copy after the caller's strong erasure/lifecycle check and lease admission.</summary>
    internal async ValueTask<PayloadKeyResolution> GetAsync(PayloadKeyCacheKey key,
        Func<CancellationToken, ValueTask<PayloadKeyResolution>> unwrap, CancellationToken cancellationToken = default, string? rootDigest = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _ = PayloadProtectionStateKeys.Key(key.ScopeDigest, key.KeyReference, key.DekVersion);
        _ = CanonicalText.GetByteCount(key.ProviderKeyVersionId, 1, 4096);
        TaskCompletionSource<UnreadableProtectedDataReason?> flight;
        bool start = false;
        lock (_gate)
        {
            if (_disposed) { throw new ObjectDisposedException(nameof(PayloadKeyCache)); }
            if (rootDigest is not null)
            {
                _ = PayloadProtectionStateKeys.Sha(rootDigest);
                if (_blockedRoots.Contains(rootDigest)) { return PayloadKeyResolution.Failure(UnreadableProtectedDataReason.KeyInvalidatedOrDeleted); }
                if (_scopeRoots.TryGetValue(key.ScopeDigest, out string? registeredRoot) && registeredRoot != rootDigest) { throw new PayloadProtectionFormatException(); }
            }
            if (_blocked.Contains(key.ScopeDigest)) { return PayloadKeyResolution.Failure(UnreadableProtectedDataReason.KeyInvalidatedOrDeleted); }
            Expire();
            if (_entries.TryGetValue(key, out PayloadKeyCacheEntry? entry)) { PayloadProtectionDiagnostics.CacheAccess(true); return Copy(key.ScopeDigest, entry); }
            PayloadProtectionDiagnostics.CacheAccess(false);
            if (!_flights.TryGetValue(key, out flight!))
            {
                if (_flights.Count >= _capacity) { return PayloadKeyResolution.Failure(UnreadableProtectedDataReason.ProviderUnavailable); }
                flight = new(TaskCreationOptions.RunContinuationsAsynchronously);
                _flights.Add(key, flight);
                start = true;
            }
            if (rootDigest is not null) { _scopeRoots[key.ScopeDigest] = rootDigest; }
        }
        if (start) { _ = PopulateAsync(key, unwrap, flight); }
        UnreadableProtectedDataReason? reason = await flight.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (reason.HasValue) { return PayloadKeyResolution.Failure(reason.Value); }
        lock (_gate)
        {
            if (_disposed || _blocked.Contains(key.ScopeDigest)) { return PayloadKeyResolution.Failure(UnreadableProtectedDataReason.KeyInvalidatedOrDeleted); }
            Expire();
            return _entries.TryGetValue(key, out PayloadKeyCacheEntry? entry) ? Copy(key.ScopeDigest, entry)
                : PayloadKeyResolution.Failure(UnreadableProtectedDataReason.ProviderUnavailable);
        }
    }

    /// <summary>Blocks the retired root, clears its cached arrays and waits for every root-owned copy and flight.</summary>
    internal async ValueTask ClearRootAsync(string root, CancellationToken cancellationToken = default)
    {
        string[] scopes;
        lock (_gate)
        {
            _blockedRoots.Add(PayloadProtectionStateKeys.Sha(root));
            scopes = _scopeRoots.Where(p => p.Value == root).Select(p => p.Key).ToArray();
        }
        foreach (string scope in scopes) { await ClearScopeAsync(scope, cancellationToken).ConfigureAwait(false); }
    }

    /// <summary>Blocks this scope, zeroes cached arrays and waits for every local key copy and unwrap to finish.</summary>
    internal async ValueTask ClearScopeAsync(string scope, CancellationToken cancellationToken = default)
    {
        Task wait;
        lock (_gate)
        {
            _blocked.Add(PayloadProtectionStateKeys.Sha(scope));
            foreach (PayloadKeyCacheKey key in _entries.Keys.Where(k => k.ScopeDigest == scope).ToArray()) { Remove(key); }
            wait = Drained(scope) ? Task.CompletedTask : NewDrainWaiter();
        }
        while (true)
        {
            await wait.WaitAsync(cancellationToken).ConfigureAwait(false);
            lock (_gate)
            {
                if (Drained(scope)) { break; }
                wait = NewDrainWaiter();
            }
        }
        cancellationToken.ThrowIfCancellationRequested();
    }

    /// <summary>Cancels shared backend work and clears every owned cache buffer, waiting for outstanding local copies.</summary>
    public ValueTask DisposeAsync()
    {
        lock (_gate) { return new(_disposeTask ??= DisposeCoreAsync()); }
    }

    private Task? _disposeTask;

    private async Task DisposeCoreAsync()
    {
        Task wait;
        lock (_gate)
        {
            _disposed = true;
            foreach (PayloadKeyCacheKey key in _entries.Keys.ToArray()) { Remove(key); }
            wait = _flights.Count == 0 && _active.Count == 0 ? Task.CompletedTask : NewDrainWaiter();
        }
        await _shutdown.CancelAsync().ConfigureAwait(false);
        while (true)
        {
            await wait.ConfigureAwait(false);
            lock (_gate)
            {
                if (_flights.Count == 0 && _active.Count == 0) { break; }
                wait = NewDrainWaiter();
            }
        }
        _shutdown.Dispose();
    }

    private async Task PopulateAsync(PayloadKeyCacheKey key, Func<CancellationToken, ValueTask<PayloadKeyResolution>> unwrap,
        TaskCompletionSource<UnreadableProtectedDataReason?> flight)
    {
        byte[]? bytes = null;
        UnreadableProtectedDataReason? failure = null;
        bool cancelled = false;
        Task<PayloadKeyResolution>? pending = null;
        try
        {
            pending = unwrap(_shutdown.Token).AsTask();
            using PayloadKeyResolution resolution = await pending.WaitAsync(_shutdown.Token).ConfigureAwait(false);
            _shutdown.Token.ThrowIfCancellationRequested();
            failure = resolution.Reason;
            if (failure is null) { bytes = resolution.TakeBytes(); }
            lock (_gate)
            {
                if (bytes is not null && !_disposed && !_blocked.Contains(key.ScopeDigest))
                {
                    Expire();
                    if (_entries.Count >= _capacity) { Remove(_entries.MinBy(p => p.Value.Ordinal).Key); }
                    _entries.Add(key, new(bytes, _clock.GetTimestamp(), checked(++_ordinal)));
                    PayloadProtectionDiagnostics.CacheEntry(true);
                    bytes = null;
                }
                else if (bytes is not null) { failure = UnreadableProtectedDataReason.KeyInvalidatedOrDeleted; }
            }
        }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested)
        {
            cancelled = true;
            if (pending is not null) { _ = DrainAbandonedAsync(pending); }
        }
        catch (PayloadProtectionStateException exception) { failure = exception.Reason; }
        catch { failure = UnreadableProtectedDataReason.ProviderUnavailable; }
        finally
        {
            if (bytes is not null) { Clear(bytes); }
            lock (_gate)
            {
                _flights.Remove(key);
                PruneRoot(key.ScopeDigest);
                if (cancelled) { flight.TrySetCanceled(); }
                else { flight.TrySetResult(failure); }
                WakeDrains();
            }
        }
    }

    private PayloadKeyResolution Copy(string scope, PayloadKeyCacheEntry entry)
    {
        byte[] copy = entry.Bytes.ToArray();
        _active[scope] = _active.GetValueOrDefault(scope) + 1;
        return PayloadKeyResolution.Success(copy, () =>
        {
            lock (_gate)
            {
                if (--_active[scope] == 0) { _active.Remove(scope); }
                PruneRoot(scope);
                WakeDrains();
            }
        });
    }

    private void Expire()
    {
        foreach (PayloadKeyCacheKey key in _entries.Where(p => _clock.GetElapsedTime(p.Value.CreatedAt) >= _ttl).Select(p => p.Key).ToArray()) { Remove(key); }
    }
    private void Remove(PayloadKeyCacheKey key)
    {
        if (_entries.Remove(key, out PayloadKeyCacheEntry? entry)) { Clear(entry.Bytes); PayloadProtectionDiagnostics.CacheEntry(false); PruneRoot(key.ScopeDigest); }
    }
    private void Clear(byte[] bytes)
    {
        CryptographicOperations.ZeroMemory(bytes);
        try { _observer?.BufferCleared(SensitiveBufferKind.DataEncryptionKey, bytes); }
        catch { /* An observer cannot suppress clearing or alter a lifecycle result. */ }
    }
    private bool Drained(string scope) => !_active.ContainsKey(scope) && !_flights.Keys.Any(k => k.ScopeDigest == scope);
    private void PruneRoot(string scope)
    {
        if (Drained(scope) && !_entries.Keys.Any(k => k.ScopeDigest == scope)) { _scopeRoots.Remove(scope); }
    }
    private Task NewDrainWaiter()
    {
        var waiter = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _drainWaiters.Add(waiter);
        return waiter.Task;
    }
    private void WakeDrains()
    {
        foreach (TaskCompletionSource waiter in _drainWaiters) { waiter.TrySetResult(); }
        _drainWaiters.Clear();
    }

    private static async Task DrainAbandonedAsync(Task<PayloadKeyResolution> pending)
    {
        try { using PayloadKeyResolution abandoned = await pending.ConfigureAwait(false); }
        catch { /* A late provider fault is observed without formatting any private detail. */ }
    }
}
