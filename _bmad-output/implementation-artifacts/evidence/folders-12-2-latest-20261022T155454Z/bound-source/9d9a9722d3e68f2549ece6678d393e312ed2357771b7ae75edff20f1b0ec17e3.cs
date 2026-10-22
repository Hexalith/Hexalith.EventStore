namespace Hexalith.EventStore.Server.Projections;

/// <summary>
/// Validated ownership scope for projection rebuild checkpoints.
/// </summary>
/// <param name="Tenant">The tenant identifier.</param>
/// <param name="Domain">The domain identifier.</param>
/// <param name="ProjectionName">The projection name.</param>
/// <param name="AggregateId">Optional aggregate-specific scope.</param>
/// <param name="OperationId">Optional rebuild operation identity.</param>
public sealed record ProjectionRebuildCheckpointScope(
    string Tenant,
    string Domain,
    string ProjectionName,
    string? AggregateId,
    string? OperationId);
