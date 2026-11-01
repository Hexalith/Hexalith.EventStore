namespace Hexalith.EventStore.Client.Projections.Checkpointed;

/// <summary>
/// A restart-safe recovery lease persisted inside the projection record.
/// </summary>
/// <param name="WorkerId">The recovery invocation holding the lease: the worker identity plus a per-invocation suffix.</param>
/// <param name="FencingToken">The strictly increasing fencing token granted with the lease.</param>
/// <param name="RecordVersion">The record version written when the lease was granted.</param>
/// <param name="ExpiresAt">When the lease stops fencing other workers.</param>
public sealed record CheckpointedProjectionLease(
    string WorkerId,
    long FencingToken,
    long RecordVersion,
    DateTimeOffset ExpiresAt);
