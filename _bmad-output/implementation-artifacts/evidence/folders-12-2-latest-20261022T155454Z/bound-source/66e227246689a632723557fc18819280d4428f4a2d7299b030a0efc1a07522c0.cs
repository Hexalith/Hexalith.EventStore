namespace Hexalith.EventStore.Contracts.Replay;

/// <summary>
/// Categorical classification of aggregate replay failures. Used for ProblemDetails
/// mapping in the Admin replay surface and for operator-facing diagnostics.
/// </summary>
public enum AggregateReconstructionErrorCategory {
    /// <summary>No error. Pairs with <see cref="AggregateReconstructionStatus.Succeeded"/>.</summary>
    None = 0,

    /// <summary>The requested aggregate type or domain has no registered owner.</summary>
    UnknownAggregateType = 1,

    /// <summary>An event type name in the stream was not recognized by the domain.</summary>
    UnknownEventType = 2,

    /// <summary>An event payload could not be deserialized to the expected CLR type.</summary>
    DeserializationFailed = 3,

    /// <summary>The state type has no public Apply method for an event in the stream.</summary>
    ApplyHandlerMissing = 4,

    /// <summary>Apply executed but threw. The state up to <see cref="AggregateReconstructionResult.LastAppliedSequenceNumber"/> is preserved.</summary>
    ApplyFailed = 5,

    /// <summary>The stored event version is not supported by the registered Apply path.</summary>
    UnsupportedVersion = 6,

    /// <summary>Any failure that does not match a more specific category.</summary>
    Unexpected = 7,

    /// <summary>A required capability, evidence or protection prerequisite is unavailable.</summary>
    Hold = 8,

    /// <summary>An admitted count, payload or allocation limit was exceeded.</summary>
    Limit = 9,

    /// <summary>Immutable evidence or an admitted replay scalar disagrees.</summary>
    Conflict = 10,
}
