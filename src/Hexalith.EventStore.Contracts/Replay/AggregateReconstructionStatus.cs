namespace Hexalith.EventStore.Contracts.Replay;

/// <summary>
/// Outcome classification for an aggregate state replay request. Drives the HTTP status
/// and ProblemDetails mapping in the Admin replay surface (Failure and HTTP Semantics
/// Matrix in admin-ui-aggregate-state-replay-correctness story).
/// </summary>
public enum AggregateReconstructionStatus {
    /// <summary>All requested events were applied successfully and <see cref="AggregateReconstructionResult.StateJson"/> is authoritative.</summary>
    Succeeded = 0,

    /// <summary>Replay applied a contiguous prefix of events but stopped at <see cref="AggregateReconstructionResult.FailedSequenceNumber"/>.</summary>
    Partial = 1,

    /// <summary>Replay could not produce trustworthy state. <see cref="AggregateReconstructionResult.StateJson"/> must not be presented as authoritative.</summary>
    Failed = 2,
}
