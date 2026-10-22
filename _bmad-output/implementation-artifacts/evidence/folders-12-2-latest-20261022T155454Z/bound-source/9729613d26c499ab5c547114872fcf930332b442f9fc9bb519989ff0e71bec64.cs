namespace Hexalith.EventStore.Contracts.Security;

/// <summary>Finite post-fence bridge cohort and exact acknowledged drains. It remains in history after successor activation.</summary>
/// <param name="RepairId">Exact RepairId.</param>
/// <param name="EpochId">Exact EpochId.</param>
/// <param name="CheckpointId">Exact CheckpointId.</param>
/// <param name="Cohort">Exact Cohort.</param>
/// <param name="DrainedOperationIds">Legacy operation-only compatibility carrier; it cannot authorize complete-owner drains or successor activation.</param>
public sealed record GovernanceRepairState(string RepairId, string EpochId, string CheckpointId, IReadOnlyList<DirectoryRepairCohortItem> Cohort, IReadOnlyList<string> DrainedOperationIds)
{
    /// <summary>Gets exact independently acknowledged complete original identities. Raw operation IDs grant no drain authority.</summary>
    public IReadOnlyList<DirectoryRepairOriginalIdentity> DrainedOriginals { get; init; } = [];
}
