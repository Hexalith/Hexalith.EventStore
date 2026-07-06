namespace Hexalith.EventStore.Client.Subscriptions;

/// <summary>
/// Persisted state of a domain-event processing marker.
/// </summary>
public enum EventStoreDomainEventMarkerState {
    /// <summary>The event message is currently being processed.</summary>
    InProgress,

    /// <summary>The event message was handled or terminally skipped.</summary>
    Completed,
}
