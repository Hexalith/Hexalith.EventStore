namespace Hexalith.EventStore.Client.Subscriptions;

/// <summary>
/// Durable marker record stored by <see cref="IEventStoreDomainEventMarkerStore"/>.
/// </summary>
/// <param name="State">The marker state.</param>
/// <param name="UpdatedAt">When the marker was last updated.</param>
public sealed record EventStoreDomainEventMarkerRecord(
    EventStoreDomainEventMarkerState State,
    DateTimeOffset UpdatedAt) {
    /// <summary>
    /// Creates an in-progress marker.
    /// </summary>
    /// <param name="updatedAt">When the marker was written.</param>
    /// <returns>An in-progress marker.</returns>
    public static EventStoreDomainEventMarkerRecord InProgress(DateTimeOffset updatedAt)
        => new(EventStoreDomainEventMarkerState.InProgress, updatedAt);

    /// <summary>
    /// Creates a completed marker.
    /// </summary>
    /// <param name="updatedAt">When the marker was written.</param>
    /// <returns>A completed marker.</returns>
    public static EventStoreDomainEventMarkerRecord Completed(DateTimeOffset updatedAt)
        => new(EventStoreDomainEventMarkerState.Completed, updatedAt);
}
