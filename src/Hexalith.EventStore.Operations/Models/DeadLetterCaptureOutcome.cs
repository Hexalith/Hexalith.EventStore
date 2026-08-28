namespace Hexalith.EventStore.Operations.Models;

/// <summary>
/// Represents a durable capture outcome.
/// </summary>
public enum DeadLetterCaptureOutcome
{
    /// <summary>A new record and its index entry were committed.</summary>
    Captured,

    /// <summary>The same message id and bytes were already committed.</summary>
    Duplicate,

    /// <summary>The same message id was already committed with different bytes.</summary>
    HashConflict,

    /// <summary>
    /// The delivery can never be retained: its identity, topic, size, or hash is outside the retained bounds.
    /// </summary>
    /// <remarks>
    /// Returned rather than thrown so the capture endpoint can tell a permanent rejection of these exact bytes
    /// from a transient actor fault. A Dapr actor proxy does not preserve the remote exception type, so an
    /// exception could not carry that distinction across the actor boundary.
    /// </remarks>
    Unretainable,
}
