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
}
