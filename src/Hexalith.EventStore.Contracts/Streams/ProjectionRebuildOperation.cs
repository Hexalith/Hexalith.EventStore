namespace Hexalith.EventStore.Contracts.Streams;

/// <summary>
/// Public status representation of an operator-triggered projection rebuild operation.
/// </summary>
/// <param name="OperationId">The rebuild operation identity.</param>
/// <param name="Tenant">The tenant that owns the operation.</param>
/// <param name="Domain">The domain that owns the operation.</param>
/// <param name="ProjectionName">The projection name.</param>
/// <param name="AggregateId">Optional aggregate scope.</param>
/// <param name="Status">The current lifecycle status.</param>
/// <param name="Checkpoint">The latest checkpoint/progress snapshot.</param>
/// <param name="StartedAt">The operation start timestamp, or <see langword="null"/> when the operation has never started.</param>
/// <param name="CompletedAt">Optional terminal timestamp.</param>
/// <param name="FailureReasonCode">Optional stable failure reason code.</param>
public sealed record ProjectionRebuildOperation(
    string OperationId,
    string Tenant,
    string Domain,
    string ProjectionName,
    string? AggregateId,
    ProjectionRebuildStatus Status,
    ProjectionRebuildCheckpoint? Checkpoint,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    string? FailureReasonCode);
