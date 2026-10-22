namespace Hexalith.EventStore.Contracts.Streams;

/// <summary>
/// Public checkpoint/progress snapshot for a projection rebuild.
/// </summary>
/// <param name="Tenant">The tenant that owns the checkpoint.</param>
/// <param name="Domain">The domain that owns the checkpoint.</param>
/// <param name="ProjectionName">The projection name or rebuild scope.</param>
/// <param name="AggregateId">Optional aggregate scope for aggregate-specific rebuilds.</param>
/// <param name="OperationId">Optional rebuild operation identity.</param>
/// <param name="LastAppliedSequence">The last sequence accepted by the projection apply path.</param>
/// <param name="Status">The rebuild lifecycle status.</param>
/// <param name="UpdatedAt">The last checkpoint update timestamp.</param>
/// <param name="FailureReasonCode">Optional stable failure reason code.</param>
/// <param name="ToPosition">Optional inclusive target position for bounded rebuild operations.</param>
public sealed record ProjectionRebuildCheckpoint(
    string Tenant,
    string Domain,
    string ProjectionName,
    string? AggregateId,
    string? OperationId,
    long LastAppliedSequence,
    ProjectionRebuildStatus Status,
    DateTimeOffset UpdatedAt,
    string? FailureReasonCode,
    long? ToPosition = null);
