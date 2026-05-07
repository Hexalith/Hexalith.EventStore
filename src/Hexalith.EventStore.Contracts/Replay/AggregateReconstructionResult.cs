namespace Hexalith.EventStore.Contracts.Replay;

/// <summary>
/// Wire result returned by a domain service replay endpoint. The Admin replay path maps
/// <see cref="Status"/> and <see cref="ErrorCategory"/> to RFC 7807 ProblemDetails per the
/// Failure and HTTP Semantics Matrix in admin-ui-aggregate-state-replay-correctness.
/// </summary>
/// <param name="Status">Overall outcome (<see cref="AggregateReconstructionStatus"/>).</param>
/// <param name="StateJson">
/// Serialized aggregate state. Authoritative for <see cref="AggregateReconstructionStatus.Succeeded"/>.
/// For <see cref="AggregateReconstructionStatus.Partial"/> it represents the state at <see cref="LastAppliedSequenceNumber"/>
/// and must never be presented as final state. Null for <see cref="AggregateReconstructionStatus.Failed"/>.
/// </param>
/// <param name="LastAppliedSequenceNumber">Last sequence number successfully applied. Zero when no event was applied.</param>
/// <param name="FailedSequenceNumber">Sequence number of the event that produced the failure when applicable.</param>
/// <param name="FailedEventType">Event type name of the event that produced the failure when applicable.</param>
/// <param name="ErrorCategory">Categorical classification used for ProblemDetails extension fields.</param>
/// <param name="Message">Operator-facing message. Must not include secrets, raw stack traces, or unsafe payload excerpts.</param>
/// <param name="Timeline">Per-event state snapshots when the request opted into timeline mode; null otherwise.</param>
public sealed record AggregateReconstructionResult(
    AggregateReconstructionStatus Status,
    string? StateJson,
    long LastAppliedSequenceNumber,
    long? FailedSequenceNumber,
    string? FailedEventType,
    AggregateReconstructionErrorCategory ErrorCategory,
    string? Message,
    IReadOnlyList<AggregateReconstructionTimelineEntry>? Timeline)
{
    /// <summary>Convenience factory for the success path.</summary>
    public static AggregateReconstructionResult Succeeded(
        string stateJson,
        long lastAppliedSequenceNumber,
        IReadOnlyList<AggregateReconstructionTimelineEntry>? timeline = null) =>
        new(
            Status: AggregateReconstructionStatus.Succeeded,
            StateJson: stateJson,
            LastAppliedSequenceNumber: lastAppliedSequenceNumber,
            FailedSequenceNumber: null,
            FailedEventType: null,
            ErrorCategory: AggregateReconstructionErrorCategory.None,
            Message: null,
            Timeline: timeline);

    /// <summary>Convenience factory for partial replay (state preserved up to the last good event).</summary>
    public static AggregateReconstructionResult Partial(
        string? stateJson,
        long lastAppliedSequenceNumber,
        long failedSequenceNumber,
        string? failedEventType,
        AggregateReconstructionErrorCategory errorCategory,
        string message,
        IReadOnlyList<AggregateReconstructionTimelineEntry>? timeline = null) =>
        new(
            Status: AggregateReconstructionStatus.Partial,
            StateJson: stateJson,
            LastAppliedSequenceNumber: lastAppliedSequenceNumber,
            FailedSequenceNumber: failedSequenceNumber,
            FailedEventType: failedEventType,
            ErrorCategory: errorCategory,
            Message: message,
            Timeline: timeline);

    /// <summary>Convenience factory for outright failure (no trustworthy state).</summary>
    public static AggregateReconstructionResult Failed(
        AggregateReconstructionErrorCategory errorCategory,
        string message,
        long? failedSequenceNumber = null,
        string? failedEventType = null,
        long lastAppliedSequenceNumber = 0) =>
        new(
            Status: AggregateReconstructionStatus.Failed,
            StateJson: null,
            LastAppliedSequenceNumber: lastAppliedSequenceNumber,
            FailedSequenceNumber: failedSequenceNumber,
            FailedEventType: failedEventType,
            ErrorCategory: errorCategory,
            Message: message,
            Timeline: null);
}
