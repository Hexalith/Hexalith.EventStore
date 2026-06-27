namespace Hexalith.EventStore.Client.Subscriptions;

/// <summary>
/// Represents the outcome of processing a consumed domain event.
/// </summary>
public enum EventStoreDomainEventProcessingResult {
    /// <summary>The event was dispatched to at least one handler.</summary>
    Processed,

    /// <summary>The event message ID was already processed or is currently being processed.</summary>
    Duplicate,

    /// <summary>The event type is not recognized by the current service and was intentionally skipped.</summary>
    SkippedUnknownEventType,

    /// <summary>The event type is known, but no handlers were registered for it.</summary>
    SkippedNoHandlers,

    /// <summary>
    /// The payload's configured identity property does not match the aggregate ID of the stream the event
    /// was delivered on. Expected when a single pub/sub topic carries events from multiple aggregate types
    /// whose identity conventions differ; the event is not addressed to this projection and is skipped
    /// (acknowledged, not retried).
    /// </summary>
    SkippedAggregateMismatch,

    /// <summary>The event payload could not be deserialized into the resolved event type, or failed an integrity check.</summary>
    FailedInvalidPayload,
}
