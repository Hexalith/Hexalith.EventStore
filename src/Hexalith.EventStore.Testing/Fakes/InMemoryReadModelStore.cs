using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;

using Hexalith.EventStore.Client.Projections;

using Microsoft.Extensions.Logging.Abstractions;

namespace Hexalith.EventStore.Testing.Fakes;

/// <summary>
/// In-memory test double for <see cref="IReadModelStore"/> and the additive
/// <see cref="IReadModelBatchStore"/> with realistic ETag / first-write-wins semantics, so both the
/// optimistic-concurrency retry loop in <see cref="ReadModelWritePolicy"/> and the coordinated batch
/// protocol can be exercised without a DAPR sidecar.
/// </summary>
/// <remarks>
/// <para>
/// Values are round-tripped through JSON on read and write to mimic DAPR serialization (no reference
/// aliasing between the stored value and what callers mutate). Batches run the exact same protocol engine
/// as the DAPR adapter over an in-memory byte accessor, so the fake models durable marker/envelope
/// transitions rather than faking atomicity — its observable outcomes match production for the same
/// scenario.
/// </para>
/// <para>
/// Use <see cref="ConcurrentWriteBeforeTrySave"/> / <see cref="ConcurrentWriteBeforeTryErase"/> to inject a
/// competing single-key write, <see cref="BatchOptions"/> to select per-store profiles and limits, and
/// <see cref="BatchFaultHook"/> to force a crash, cancellation, or partial progress at an exact batch
/// protocol phase.
/// </para>
/// </remarks>
public sealed class InMemoryReadModelStore : IReadModelStore, IReadModelBatchStore, IReadModelBatchStagingStore, IReadModelConditionalEraser {
    private static readonly JsonSerializerOptions s_json = new(JsonSerializerDefaults.Web);

    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private readonly Lock _gate = new();
    private long _etagSequence;

    /// <summary>
    /// Gets or sets a callback invoked at the start of every <see cref="TrySaveAsync{TValue}"/> call,
    /// before the ETag is compared. Use it to simulate a concurrent writer (e.g. by calling
    /// <see cref="SeedRaw"/>) and then clear it so the subsequent retry succeeds.
    /// </summary>
    public Action? ConcurrentWriteBeforeTrySave { get; set; }

    /// <summary>
    /// Gets or sets a callback invoked at the start of every <see cref="TryEraseAsync"/> call,
    /// before the ETag is compared. Use it to simulate a concurrent writer or a deterministic
    /// partial failure, then clear it so a subsequent retry can succeed.
    /// </summary>
    public Action? ConcurrentWriteBeforeTryErase { get; set; }

    /// <summary>Gets the coordinated-batch options and per-store profiles used by <see cref="ExecuteAsync"/>.</summary>
    public ReadModelBatchOptions BatchOptions { get; } = new();

    /// <summary>
    /// Gets or sets a deterministic batch fault hook invoked at each protocol phase. Throw from it to
    /// simulate a crash/transport failure, or cancel to simulate post-dispatch cancellation.
    /// </summary>
    public Func<ReadModelBatchPhase, int, CancellationToken, Task>? BatchFaultHook { get; set; }

    /// <summary>Gets the number of stored keys across all stores (including batch markers/envelopes).</summary>
    public int Count => _entries.Count;

    /// <inheritdoc/>
    public async Task<ReadModelEntry<TValue>> GetAsync<TValue>(
        string storeName,
        string key,
        CancellationToken cancellationToken = default)
        where TValue : class {
        ArgumentException.ThrowIfNullOrWhiteSpace(storeName);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        cancellationToken.ThrowIfCancellationRequested();

        var accessor = new InMemoryBatchAccessor(this, storeName);
        RawStateEntry visible = await ReadModelBatchProtocol
            .ResolveVisibleAsync(accessor, key, cancellationToken)
            .ConfigureAwait(false);
        return visible.Exists
            ? new ReadModelEntry<TValue>(Deserialize<TValue>(visible.Value), visible.ETag)
            : new ReadModelEntry<TValue>(null, null);
    }

    /// <inheritdoc/>
    public Task SaveAsync<TValue>(
        string storeName,
        string key,
        TValue value,
        CancellationToken cancellationToken = default)
        where TValue : class {
        ArgumentException.ThrowIfNullOrWhiteSpace(storeName);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);
        cancellationToken.ThrowIfCancellationRequested();

        _entries[Compose(storeName, key)] = new Entry(Serialize(value), NextETag());
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<bool> TrySaveAsync<TValue>(
        string storeName,
        string key,
        TValue value,
        string etag,
        CancellationToken cancellationToken = default)
        where TValue : class {
        ArgumentException.ThrowIfNullOrWhiteSpace(storeName);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(etag);
        cancellationToken.ThrowIfCancellationRequested();

        ConcurrentWriteBeforeTrySave?.Invoke();

        string composite = Compose(storeName, key);
        bool exists = _entries.TryGetValue(composite, out Entry? current);

        // First-write-wins: a create requires an empty ETag and an absent key; an update requires the
        // caller-held ETag to still match the stored one.
        bool matches = exists
            ? string.Equals(current!.ETag, etag, StringComparison.Ordinal)
            : etag.Length == 0;
        if (!matches) {
            return Task.FromResult(false);
        }

        _entries[composite] = new Entry(Serialize(value), NextETag());
        return Task.FromResult(true);
    }

    /// <inheritdoc/>
    public Task<bool> TryEraseAsync(
        string storeName,
        string key,
        string etag,
        CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(storeName);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(etag);
        cancellationToken.ThrowIfCancellationRequested();

        ConcurrentWriteBeforeTryErase?.Invoke();

        string composite = Compose(storeName, key);
        if (!_entries.TryGetValue(composite, out Entry? current)) {
            return Task.FromResult(true);
        }

        if (!string.Equals(current.ETag, etag, StringComparison.Ordinal)) {
            return Task.FromResult(false);
        }

        bool removed = ((ICollection<KeyValuePair<string, Entry>>)_entries)
            .Remove(new KeyValuePair<string, Entry>(composite, current));
        return Task.FromResult(removed);
    }

    /// <inheritdoc/>
    public Task<(bool Present, string Etag)> TryReadEtagAsync(
        string storeName,
        string key,
        CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(storeName);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        cancellationToken.ThrowIfCancellationRequested();

        // Read the raw backing entry so the returned ETag matches the one TryEraseAsync compares against.
        return _entries.TryGetValue(Compose(storeName, key), out Entry? entry)
            ? Task.FromResult((true, entry.ETag))
            : Task.FromResult((false, string.Empty));
    }

    /// <inheritdoc/>
    public async Task<ReadModelBatchResult> ExecuteAsync(
        ReadModelBatch batch,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(batch);

        var accessor = new InMemoryBatchAccessor(this, batch.Scope.StoreName);
        IReadModelBatchFaultInjector? injector = BatchFaultHook is null
            ? null
            : new DelegateFaultInjector(BatchFaultHook);
        var protocol = new ReadModelBatchProtocol(accessor, BatchOptions, NullLogger.Instance);
        return await protocol.ExecuteAsync(batch, injector, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public Task<ReadModelBatchStagingResult> StageAsync(
        ReadModelBatch batch,
        CancellationToken cancellationToken = default)
        => ExecuteStagingAsync(batch, ReadModelBatchStagingAction.Stage, cancellationToken);

    /// <inheritdoc/>
    public Task<ReadModelBatchStagingResult> CommitAsync(
        ReadModelBatch batch,
        CancellationToken cancellationToken = default)
        => ExecuteStagingAsync(batch, ReadModelBatchStagingAction.Commit, cancellationToken);

    /// <inheritdoc/>
    public Task<ReadModelBatchStagingResult> AbortAsync(
        ReadModelBatch batch,
        CancellationToken cancellationToken = default)
        => ExecuteStagingAsync(batch, ReadModelBatchStagingAction.Abort, cancellationToken);

    /// <inheritdoc/>
    public Task<ReadModelBatchStagingResult> VerifyAsync(
        ReadModelBatch batch,
        CancellationToken cancellationToken = default)
        => ExecuteStagingAsync(batch, ReadModelBatchStagingAction.Verify, cancellationToken);

    private async Task<ReadModelBatchStagingResult> ExecuteStagingAsync(
        ReadModelBatch batch,
        ReadModelBatchStagingAction action,
        CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(batch);
        var accessor = new InMemoryBatchAccessor(this, batch.Scope.StoreName);
        IReadModelBatchFaultInjector? injector = BatchFaultHook is null
            ? null
            : new DelegateFaultInjector(BatchFaultHook);
        var protocol = new ReadModelBatchProtocol(accessor, BatchOptions, NullLogger.Instance);
        return action switch {
            ReadModelBatchStagingAction.Stage => await protocol.StageAsync(batch, injector, cancellationToken).ConfigureAwait(false),
            ReadModelBatchStagingAction.Commit => await protocol.CommitStagedAsync(batch, injector, cancellationToken).ConfigureAwait(false),
            ReadModelBatchStagingAction.Abort => await protocol.AbortStagedAsync(batch, injector, cancellationToken).ConfigureAwait(false),
            _ => await protocol.VerifyStagedAsync(batch, injector, cancellationToken).ConfigureAwait(false),
        };
    }

    /// <summary>
    /// Seeds a raw value directly (assigning a fresh ETag), bypassing concurrency checks. Useful as a
    /// concurrent-writer simulation from <see cref="ConcurrentWriteBeforeTrySave"/>.
    /// </summary>
    /// <typeparam name="TValue">The read-model type.</typeparam>
    /// <param name="storeName">The store name.</param>
    /// <param name="key">The state key.</param>
    /// <param name="value">The value to seed.</param>
    public void SeedRaw<TValue>(string storeName, string key, TValue value)
        where TValue : class {
        ArgumentNullException.ThrowIfNull(value);
        _entries[Compose(storeName, key)] = new Entry(Serialize(value), NextETag());
    }

    /// <summary>
    /// Returns the currently stored value for assertions, or <see langword="null"/> when absent. Reads the
    /// raw stored bytes directly; a value hidden behind an uncommitted batch envelope is not decoded here
    /// (use <see cref="GetAsync{TValue}"/> for the marker-gated visible value).
    /// </summary>
    /// <typeparam name="TValue">The read-model type.</typeparam>
    /// <param name="storeName">The store name.</param>
    /// <param name="key">The state key.</param>
    /// <returns>The stored value, or <see langword="null"/>.</returns>
    public TValue? Snapshot<TValue>(string storeName, string key)
        where TValue : class =>
        _entries.TryGetValue(Compose(storeName, key), out Entry? entry) && !ReadModelBatchEnvelope.IsEnvelope(entry.Bytes)
            ? Deserialize<TValue>(entry.Bytes)
            : null;

    /// <summary>Returns whether the raw stored bytes at a key are a pending batch envelope.</summary>
    /// <param name="storeName">The store name.</param>
    /// <param name="key">The state key.</param>
    /// <returns><see langword="true"/> when the key holds a batch envelope.</returns>
    public bool HasPendingEnvelope(string storeName, string key) =>
        _entries.TryGetValue(Compose(storeName, key), out Entry? entry) && ReadModelBatchEnvelope.IsEnvelope(entry.Bytes);

    private static string Compose(string storeName, string key) => storeName + "\0" + key;

    private static byte[] Serialize<TValue>(TValue value) => JsonSerializer.SerializeToUtf8Bytes(value, s_json);

    private static TValue? Deserialize<TValue>(ReadOnlyMemory<byte> bytes)
        where TValue : class => JsonSerializer.Deserialize<TValue>(bytes.Span, s_json);

    private string NextETag() => Interlocked.Increment(ref _etagSequence).ToString(CultureInfo.InvariantCulture);

    private sealed record Entry(byte[] Bytes, string ETag);

    /// <summary>Raw byte-state accessor over the fake's dictionary for one store component.</summary>
    private sealed class InMemoryBatchAccessor(InMemoryReadModelStore store, string storeName)
        : IReadModelBatchStateAccessor {
        public bool SupportsTransaction => true;

        public Task<RawStateEntry> ReadAsync(string key, CancellationToken cancellationToken) {
            cancellationToken.ThrowIfCancellationRequested();
            string composite = Compose(storeName, key);
            return Task.FromResult(store._entries.TryGetValue(composite, out Entry? entry)
                ? new RawStateEntry(true, entry.Bytes, entry.ETag)
                : new RawStateEntry(false, ReadOnlyMemory<byte>.Empty, string.Empty));
        }

        public Task<bool> TryWriteAsync(string key, ReadOnlyMemory<byte> value, string expectedETag, CancellationToken cancellationToken) {
            cancellationToken.ThrowIfCancellationRequested();
            string composite = Compose(storeName, key);
            lock (store._gate) {
                bool exists = store._entries.TryGetValue(composite, out Entry? current);
                bool matches = exists
                    ? string.Equals(current!.ETag, expectedETag, StringComparison.Ordinal)
                    : expectedETag.Length == 0;
                if (!matches) {
                    return Task.FromResult(false);
                }

                store._entries[composite] = new Entry(value.ToArray(), store.NextETag());
                return Task.FromResult(true);
            }
        }

        public Task<bool> TryDeleteAsync(string key, string expectedETag, CancellationToken cancellationToken) {
            cancellationToken.ThrowIfCancellationRequested();
            string composite = Compose(storeName, key);
            lock (store._gate) {
                if (!store._entries.TryGetValue(composite, out Entry? current)
                    || !string.Equals(current.ETag, expectedETag, StringComparison.Ordinal)) {
                    return Task.FromResult(false);
                }

                bool removed = ((ICollection<KeyValuePair<string, Entry>>)store._entries)
                    .Remove(new KeyValuePair<string, Entry>(composite, current));
                return Task.FromResult(removed);
            }
        }

        public Task ExecuteTransactionAsync(IReadOnlyList<RawTransactionOperation> operations, CancellationToken cancellationToken) {
            cancellationToken.ThrowIfCancellationRequested();
            lock (store._gate) {
                // All-or-nothing: validate every first-write precondition before mutating.
                foreach (RawTransactionOperation operation in operations) {
                    if (!operation.FirstWrite) {
                        continue;
                    }

                    string composite = Compose(storeName, operation.Key);
                    bool exists = store._entries.TryGetValue(composite, out Entry? current);
                    bool matches = exists
                        ? string.Equals(current!.ETag, operation.ETag, StringComparison.Ordinal)
                        : operation.ETag.Length == 0;
                    if (!matches) {
                        throw new ReadModelBatchStoreException(
                            "In-memory state transaction rejected: first-write ETag precondition failed.");
                    }
                }

                foreach (RawTransactionOperation operation in operations) {
                    string composite = Compose(storeName, operation.Key);
                    if (operation.IsDelete) {
                        _ = store._entries.TryRemove(composite, out _);
                    }
                    else {
                        store._entries[composite] = new Entry(operation.Value.ToArray(), store.NextETag());
                    }
                }
            }

            return Task.CompletedTask;
        }
    }

    /// <summary>Adapts the public batch fault hook delegate to the internal injector interface.</summary>
    private sealed class DelegateFaultInjector(Func<ReadModelBatchPhase, int, CancellationToken, Task> hook)
        : IReadModelBatchFaultInjector {
        public Task InjectAsync(ReadModelBatchPhase phase, int ordinal, CancellationToken cancellationToken) =>
            hook(phase, ordinal, cancellationToken);
    }
}
