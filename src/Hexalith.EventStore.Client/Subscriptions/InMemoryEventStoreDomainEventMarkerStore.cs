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

        while (true) {
            // Re-check on every iteration: the loop only re-spins when a concurrent ReleaseAsync removes the
            // key between TryAdd and TryGetValue, so an inner check keeps the spin cooperative with cancellation.
            cancellationToken.ThrowIfCancellationRequested();
            if (_markers.TryAdd(messageId, EventStoreDomainEventMarkerState.InProgress)) {
                return Task.FromResult(EventStoreDomainEventMarkerAcquisitionResult.Acquired);
            }

            if (_markers.TryGetValue(messageId, out EventStoreDomainEventMarkerState state)) {
                return Task.FromResult(state switch {
                    EventStoreDomainEventMarkerState.Completed => EventStoreDomainEventMarkerAcquisitionResult.Completed,
                    EventStoreDomainEventMarkerState.InProgress => EventStoreDomainEventMarkerAcquisitionResult.InProgress,
                    EventStoreDomainEventMarkerState.Dispatched => EventStoreDomainEventMarkerAcquisitionResult.CompletionPending,
                    _ => (EventStoreDomainEventMarkerAcquisitionResult)(-1),
                });
            }
        }
    }

    /// <inheritdoc/>
    public Task<bool> MarkDispatchedAsync(string messageId, CancellationToken cancellationToken = default)
        => Task.FromResult(Transition(messageId, EventStoreDomainEventMarkerState.Dispatched, cancellationToken));

    /// <inheritdoc/>
    public Task MarkCompletedAsync(string messageId, CancellationToken cancellationToken = default) {
        _ = Transition(messageId, EventStoreDomainEventMarkerState.Completed, cancellationToken);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task ReleaseAsync(string messageId, CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
        cancellationToken.ThrowIfCancellationRequested();

        _ = ((ICollection<KeyValuePair<string, EventStoreDomainEventMarkerState>>)_markers)
            .Remove(new KeyValuePair<string, EventStoreDomainEventMarkerState>(messageId, EventStoreDomainEventMarkerState.InProgress));
        return Task.CompletedTask;
    }

    private bool Transition(
        string messageId,
        EventStoreDomainEventMarkerState targetState,
        CancellationToken cancellationToken) {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);

        while (true) {
            cancellationToken.ThrowIfCancellationRequested();
            if (!_markers.TryGetValue(messageId, out EventStoreDomainEventMarkerState existing)) {
                if (_markers.TryAdd(messageId, targetState)) {
                    return targetState == EventStoreDomainEventMarkerState.Dispatched;
                }

                continue;
            }

            switch (existing) {
                case EventStoreDomainEventMarkerState.Completed:
                    return false;
                case EventStoreDomainEventMarkerState.Dispatched when targetState == EventStoreDomainEventMarkerState.Dispatched:
                    return true;
                case EventStoreDomainEventMarkerState.Dispatched:
                case EventStoreDomainEventMarkerState.InProgress:
                    if (_markers.TryUpdate(messageId, targetState, existing)) {
                        return targetState == EventStoreDomainEventMarkerState.Dispatched;
                    }

                    break;
                default:
                    throw new InvalidOperationException(
                        $"Cannot transition marker for message '{messageId}' from unsupported state '{existing}' to '{targetState}'.");
            }
        }
    }
}
