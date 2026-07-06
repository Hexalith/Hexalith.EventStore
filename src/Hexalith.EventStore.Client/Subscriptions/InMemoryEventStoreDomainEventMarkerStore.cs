using System.Collections.Concurrent;

namespace Hexalith.EventStore.Client.Subscriptions;

/// <summary>
/// Deterministic in-memory implementation of <see cref="IEventStoreDomainEventMarkerStore"/> for tests.
/// </summary>
public sealed class InMemoryEventStoreDomainEventMarkerStore : IEventStoreDomainEventMarkerStore {
    private readonly ConcurrentDictionary<string, EventStoreDomainEventMarkerState> _markers = new(StringComparer.Ordinal);

    /// <inheritdoc/>
    public Task<EventStoreDomainEventMarkerAcquisitionResult> TryAcquireAsync(
        string messageId,
        CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
        cancellationToken.ThrowIfCancellationRequested();

        while (true) {
            if (_markers.TryAdd(messageId, EventStoreDomainEventMarkerState.InProgress)) {
                return Task.FromResult(EventStoreDomainEventMarkerAcquisitionResult.Acquired);
            }

            if (_markers.TryGetValue(messageId, out EventStoreDomainEventMarkerState state)) {
                return Task.FromResult(state == EventStoreDomainEventMarkerState.Completed
                    ? EventStoreDomainEventMarkerAcquisitionResult.Completed
                    : EventStoreDomainEventMarkerAcquisitionResult.InProgress);
            }
        }
    }

    /// <inheritdoc/>
    public Task MarkCompletedAsync(string messageId, CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
        cancellationToken.ThrowIfCancellationRequested();

        _markers[messageId] = EventStoreDomainEventMarkerState.Completed;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task ReleaseAsync(string messageId, CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
        cancellationToken.ThrowIfCancellationRequested();

        _ = _markers.TryRemove(messageId, out _);
        return Task.CompletedTask;
    }
}
