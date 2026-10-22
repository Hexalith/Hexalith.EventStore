namespace Hexalith.EventStore.Server.Events;

/// <summary>
/// Thrown when an event cannot be deserialized from the state store, indicating corrupt data or schema issues.
/// </summary>
public class EventDeserializationException : InvalidOperationException {
    /// <summary>
    /// Initializes a new instance of the <see cref="EventDeserializationException"/> class.
    /// </summary>
    /// <param name="sequenceNumber">The event sequence number.</param>
    /// <param name="actorId">The actor identifier.</param>
    /// <param name="innerException">The deserialization exception.</param>
    public EventDeserializationException(long sequenceNumber, string actorId, Exception innerException)
        : base($"Failed to deserialize event at sequence {sequenceNumber} for {actorId}: {(innerException ?? throw new ArgumentNullException(nameof(innerException))).Message}", innerException) {
        SequenceNumber = sequenceNumber;
        ActorId = actorId;
    }

    /// <summary>Gets the event sequence number.</summary>
    public long SequenceNumber { get; }

    /// <summary>Gets the actor identifier.</summary>
    public string ActorId { get; }
}
