namespace Hexalith.EventStore.Client.Subscriptions;

/// <summary>
/// Describes the outcome of attempting to acquire a domain-event processing marker.
/// </summary>
public enum EventStoreDomainEventMarkerAcquisitionResult {
    /// <summary>The caller acquired the marker and may process the event.</summary>
    Acquired = 0,

    /// <summary>The event message was already completed and must be acknowledged as a duplicate.</summary>
    Completed = 1,

    /// <summary>Another processing attempt owns the marker; the delivery should remain retryable.</summary>
    InProgress = 2,

    /// <summary>Handlers already ran and the caller must only complete the durable marker.</summary>
    CompletionPending = 3,
}
