namespace Hexalith.EventStore.Client.Subscriptions;

/// <summary>
/// Describes the outcome of attempting to acquire a domain-event processing marker.
/// </summary>
public enum EventStoreDomainEventMarkerAcquisitionResult {
    /// <summary>The caller acquired the marker and may process the event.</summary>
    Acquired,

    /// <summary>The event message was already completed and must be acknowledged as a duplicate.</summary>
    Completed,

    /// <summary>Another processing attempt owns the marker; the delivery should remain retryable.</summary>
    InProgress,
}
