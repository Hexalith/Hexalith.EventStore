namespace Hexalith.EventStore.Server.Projections;

/// <summary>
/// Persisted checkpoint for server-managed projection delivery.
/// </summary>
/// <param name="TenantId">The tenant identifier.</param>
/// <param name="Domain">The aggregate domain.</param>
/// <param name="AggregateId">The aggregate identifier.</param>
/// <param name="LastDeliveredSequence">The highest event sequence delivered successfully.</param>
/// <param name="UpdatedAt">The UTC time when the checkpoint was written.</param>
public sealed record ProjectionCheckpoint(
    string TenantId,
    string Domain,
    string AggregateId,
    long LastDeliveredSequence,
    DateTimeOffset UpdatedAt);
