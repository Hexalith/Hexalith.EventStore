using System.Collections.Concurrent;

using Hexalith.EventStore.Server.Configuration;

using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Server.Commands;

/// <summary>
/// In-memory implementation of <see cref="IBackpressureTracker"/> using a <see cref="ConcurrentDictionary{TKey,TValue}"/>
/// with atomic compare-and-swap operations for thread-safe counter management.
/// Each API instance tracks its own in-flight command counts (acceptable approximation for multi-instance deployments).
/// </summary>
public class InMemoryBackpressureTracker : IBackpressureTracker {
    private readonly ConcurrentDictionary<string, int> _counters = new(StringComparer.Ordinal);
    private readonly int _threshold;

    public InMemoryBackpressureTracker(IOptions<BackpressureOptions> options) {
        ArgumentNullException.ThrowIfNull(options);
        _threshold = options.Value.MaxPendingCommandsPerAggregate;
    }

    /// <inheritdoc />
    public bool TryAcquire(string aggregateActorId) {
        ArgumentException.ThrowIfNullOrWhiteSpace(aggregateActorId);

        // Threshold 0 = disabled — always allow
        if (_threshold == 0) {
            return true;
        }

        // Spin-loop with compare-and-swap for thread-safe increment-check-rollback
        while (true) {
            int current = _counters.GetOrAdd(aggregateActorId, 0);

            if (current >= _threshold) {
                return false;
            }

            if (_counters.TryUpdate(aggregateActorId, current + 1, current)) {
                return true;
            }

            // Another thread changed the value — retry
        }
    }

    /// <inheritdoc />
    public int GetCurrentDepth(string aggregateActorId) {
        ArgumentException.ThrowIfNullOrWhiteSpace(aggregateActorId);
        return _counters.TryGetValue(aggregateActorId, out int current) ? current : 0;
    }

    /// <inheritdoc />
    public void Release(string aggregateActorId) {
        ArgumentException.ThrowIfNullOrWhiteSpace(aggregateActorId);

        // Threshold 0 = disabled — TryAcquire never touches dictionary, so nothing to release
        if (_threshold == 0) {
            return;
        }

        // Decrement with floor at 0 and remove key when reaching 0
        while (true) {
            if (!_counters.TryGetValue(aggregateActorId, out int current)) {
                // Already removed or never existed — floor at 0
                return;
            }

            if (current <= 0) {
                // Floor at 0 — try to remove the stale key
                _ = ((ICollection<KeyValuePair<string, int>>)_counters).Remove(new KeyValuePair<string, int>(aggregateActorId, current));
                return;
            }

            if (current == 1) {
                // Decrementing to 0 — remove the key entirely to prevent unbounded growth
                if (((ICollection<KeyValuePair<string, int>>)_counters).Remove(new KeyValuePair<string, int>(aggregateActorId, current))) {
                    return;
                }

                // Another thread changed the value — retry
                continue;
            }

            // Decrement atomically
            if (_counters.TryUpdate(aggregateActorId, current - 1, current)) {
                return;
            }

            // Another thread changed the value — retry
        }
    }

    /// <summary>
    /// Exposes the internal counter count for testing purposes only.
    /// </summary>
    internal int GetEntryCount() => _counters.Count;
}
