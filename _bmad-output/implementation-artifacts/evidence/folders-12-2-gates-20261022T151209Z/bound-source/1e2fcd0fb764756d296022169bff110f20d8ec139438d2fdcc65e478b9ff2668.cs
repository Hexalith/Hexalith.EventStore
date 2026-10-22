namespace Hexalith.EventStore.Contracts.Security;

/// <summary>Append-only technical epoch/checkpoint/drain/outcome retention; activation retains every predecessor fact and referenced guard evidence.</summary>
/// <param name="TenantId">Exact tenant.</param><param name="Revision">Current durable metadata revision.</param><param name="CurrentEpochId">Current installed epoch.</param>
/// <param name="ActiveRepairId">Restrictive repair boundary, or null only after exact activation.</param><param name="Installations">Immutable initial and successor installations.</param>
/// <param name="Repairs">All immutable finite cohorts.</param><param name="Drains">All exact original results.</param><param name="Activations">Every predecessor preservation/revocation proof.</param>
/// <param name="Outcomes">Gap-free immutable conditional outcomes.</param>
public sealed record DirectoryEpochLedger(string TenantId, long Revision, string? CurrentEpochId, string? ActiveRepairId,
    IReadOnlyList<DirectoryEpochInstallation> Installations, IReadOnlyList<DirectoryRepairBoundary> Repairs,
    IReadOnlyList<DirectoryRepairDrainReceipt> Drains, IReadOnlyList<DirectoryEpochActivation> Activations, IReadOnlyList<DirectoryBoundaryOutcome> Outcomes);
