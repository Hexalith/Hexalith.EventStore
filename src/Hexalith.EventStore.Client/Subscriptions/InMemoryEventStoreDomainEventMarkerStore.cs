namespace Hexalith.EventStore.Client.Subscriptions;

/// <summary>Bounded development-only processing markers for a single subscription.</summary>
public sealed class InMemoryEventStoreDomainEventMarkerStore : IEventStoreDomainEventMarkerStore
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, EventStoreDomainEventMarkerRecord> _markers = new(StringComparer.Ordinal);
    private readonly PriorityQueue<string, DateTimeOffset> _completed = new();
    private readonly int _capacity;
    private readonly TimeSpan _retention;
    private readonly TimeProvider _time;

    /// <summary>Creates a store with the default development bounds.</summary>
    public InMemoryEventStoreDomainEventMarkerStore() : this(new EventStoreDomainEventsOptions())
    {
    }

    /// <summary>Creates a development store with configured bounds.</summary>
    /// <param name="options">The subscription's marker options.</param>
    /// <param name="timeProvider">The optional clock.</param>
    public InMemoryEventStoreDomainEventMarkerStore(EventStoreDomainEventsOptions options, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.DevelopmentMarkerCapacity, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(options.DevelopmentMarkerCapacity, 1000000);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(options.DevelopmentMarkerRetention, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(options.DevelopmentMarkerRetention, TimeSpan.FromDays(365));
        _capacity = options.DevelopmentMarkerCapacity;
        _retention = options.DevelopmentMarkerRetention;
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc/>
    public bool SupportsContentBoundAdmission => true;

    /// <inheritdoc/>
    public Task<EventStoreDomainEventMarkerAdmission> TryAcquireContentAsync(
        string messageId, string fingerprint, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fingerprint);
        return Task.FromResult(Acquire(messageId, fingerprint, cancellationToken));
    }

    /// <inheritdoc/>
    public Task<EventStoreDomainEventMarkerAcquisitionResult> TryAcquireAsync(string messageId, CancellationToken cancellationToken = default)
        => Task.FromResult(Acquire(messageId, null, cancellationToken).Result);

    /// <inheritdoc/>
    public Task<bool> MarkFencedDispatchedAsync(EventStoreDomainEventMarkerLease lease, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lease);
        return Task.FromResult(Transition(lease.MessageId, lease, EventStoreDomainEventMarkerState.Dispatched, cancellationToken));
    }

    /// <inheritdoc/>
    public Task MarkFencedCompletedAsync(EventStoreDomainEventMarkerLease lease, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lease);
        _ = Transition(lease.MessageId, lease, EventStoreDomainEventMarkerState.Completed, cancellationToken);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task ReleaseFenceAsync(EventStoreDomainEventMarkerLease lease, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lease);
        return ReleaseAsync(lease.MessageId, lease, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<bool> MarkDispatchedAsync(string messageId, CancellationToken cancellationToken = default)
        => Task.FromResult(Transition(messageId, null, EventStoreDomainEventMarkerState.Dispatched, cancellationToken));

    /// <inheritdoc/>
    public Task MarkCompletedAsync(string messageId, CancellationToken cancellationToken = default)
    {
        _ = Transition(messageId, null, EventStoreDomainEventMarkerState.Completed, cancellationToken);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task ReleaseAsync(string messageId, CancellationToken cancellationToken = default)
        => ReleaseAsync(messageId, null, cancellationToken);

    private EventStoreDomainEventMarkerAdmission Acquire(string messageId, string? fingerprint, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
        lock (_gate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DateTimeOffset now = _time.GetUtcNow();
            Compact(now);
            if (_markers.TryGetValue(messageId, out EventStoreDomainEventMarkerRecord? current))
            {
                if (fingerprint is not null && (current.Fingerprint != fingerprint || current.Fence == Guid.Empty))
                {
                    return new(EventStoreDomainEventMarkerAcquisitionResult.ContentConflict);
                }

                return current.State switch
                {
                    EventStoreDomainEventMarkerState.Completed => new(EventStoreDomainEventMarkerAcquisitionResult.Completed),
                    EventStoreDomainEventMarkerState.Dispatched => new(EventStoreDomainEventMarkerAcquisitionResult.CompletionPending,
                        fingerprint is null ? null : new(messageId, fingerprint, current.Fence)),
                    _ => new(EventStoreDomainEventMarkerAcquisitionResult.InProgress),
                };
            }

            if (_markers.Count >= _capacity)
            {
                return new(EventStoreDomainEventMarkerAcquisitionResult.InProgress);
            }

            var lease = fingerprint is null ? null : new EventStoreDomainEventMarkerLease(messageId, fingerprint, Guid.NewGuid());
            _markers.Add(messageId, EventStoreDomainEventMarkerRecord.InProgress(now) with
            {
                Fingerprint = fingerprint,
                Fence = lease?.Fence ?? Guid.Empty,
            });
            return new(EventStoreDomainEventMarkerAcquisitionResult.Acquired, lease);
        }
    }

    private bool Transition(string messageId, EventStoreDomainEventMarkerLease? lease,
        EventStoreDomainEventMarkerState destination, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
        lock (_gate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DateTimeOffset now = _time.GetUtcNow();
            if (!_markers.TryGetValue(messageId, out EventStoreDomainEventMarkerRecord? current))
            {
                Compact(now);
                if (lease is not null || _markers.Count >= _capacity)
                {
                    throw new InvalidOperationException("The marker cannot be advanced.");
                }

                current = EventStoreDomainEventMarkerRecord.InProgress(now);
            }

            if (!Owns(current, lease) || (lease is not null && destination == EventStoreDomainEventMarkerState.Completed
                    && current.State == EventStoreDomainEventMarkerState.InProgress))
            {
                throw new InvalidOperationException("The marker cannot be advanced by this attempt.");
            }

            if (current.State == EventStoreDomainEventMarkerState.Completed || current.State == destination)
            {
                return current.State == EventStoreDomainEventMarkerState.Dispatched;
            }

            _markers[messageId] = current with { State = destination, UpdatedAt = now };
            if (destination == EventStoreDomainEventMarkerState.Completed)
            {
                _completed.Enqueue(messageId, now);
            }

            return destination == EventStoreDomainEventMarkerState.Dispatched;
        }
    }

    private Task ReleaseAsync(string messageId, EventStoreDomainEventMarkerLease? lease, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
        lock (_gate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_markers.TryGetValue(messageId, out EventStoreDomainEventMarkerRecord? current)
                && current.State == EventStoreDomainEventMarkerState.InProgress && Owns(current, lease))
            {
                _ = _markers.Remove(messageId);
            }
        }

        return Task.CompletedTask;
    }

    private void Compact(DateTimeOffset now)
    {
        while (_completed.TryPeek(out string? key, out DateTimeOffset completedAt) && now - completedAt >= _retention)
        {
            _ = _completed.Dequeue();
            _ = _markers.Remove(key);
        }
    }

    private static bool Owns(EventStoreDomainEventMarkerRecord current, EventStoreDomainEventMarkerLease? lease)
        => current.Fingerprint == lease?.Fingerprint && current.Fence == (lease?.Fence ?? Guid.Empty);
}
