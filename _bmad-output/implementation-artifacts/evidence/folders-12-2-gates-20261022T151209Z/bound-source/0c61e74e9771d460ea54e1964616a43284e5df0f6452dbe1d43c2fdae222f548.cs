namespace Hexalith.EventStore.Contracts.Security;

/// <summary>Candidate finite complete private registry; independently qualified epoch/monotonic anchor denies rollback or restored stale state.</summary>
/// <param name="TenantId">Exact tenant.</param>
/// <param name="EpochId">Independently provisioned registry epoch.</param>
/// <param name="Revision">Exact durable monotonic revision.</param>
/// <param name="Records">All used original references, including aborted ones.</param>
public sealed record InteractionOccurrenceRegistrySnapshot(string TenantId, string EpochId, long Revision, IReadOnlyList<InteractionOccurrenceRecord> Records)
{
    /// <inheritdoc/>
    public override string ToString() => nameof(InteractionOccurrenceRegistrySnapshot);
}
