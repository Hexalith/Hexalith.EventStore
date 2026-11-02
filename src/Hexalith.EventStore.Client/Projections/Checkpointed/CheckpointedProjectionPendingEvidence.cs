namespace Hexalith.EventStore.Client.Projections.Checkpointed;

/// <summary>
/// Durable evidence observed for a delivery beyond the contiguous checkpoint of a gapped projection.
/// </summary>
/// <param name="Sequence">The aggregate sequence of the observed delivery.</param>
/// <param name="Evidence">The delivered event identity at that sequence.</param>
public sealed record CheckpointedProjectionPendingEvidence(
    long Sequence,
    CheckpointedProjectionEvidence Evidence);
