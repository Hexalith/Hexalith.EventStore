namespace Hexalith.EventStore.Client.Subscriptions;

/// <summary>Closed failure categories accepted by the subscriber poison sink.</summary>
public enum EventStoreSubscriberFailureReason
{
    /// <summary>The payload was rejected before dispatch.</summary>
    InvalidPayload = 1,
    /// <summary>No catalog policy authorized acknowledgement of the unknown event type.</summary>
    UnknownEventType,
    /// <summary>No handler or catalog skip policy was available.</summary>
    NoHandlers,
    /// <summary>The payload identity disagreed with the stream identity.</summary>
    AggregateMismatch,
    /// <summary>The required event handling capability was unavailable.</summary>
    CapabilityUnavailable,
    /// <summary>A marker is owned or its effects are ambiguous.</summary>
    MarkerPending,
    /// <summary>An existing marker and the delivery disagree.</summary>
    ContentConflict,
    /// <summary>A handler or state operation returned an uncertain outcome.</summary>
    ProcessingFailure,
    /// <summary>The application-level maximum delivery age elapsed.</summary>
    DeliveryAgeExceeded,
}
