namespace Hexalith.EventStore.Contracts.Security;

/// <summary>Independent exact terminal result for one original finite-cohort operation; no fresh effect authorization.</summary>
/// <param name="TenantId">Exact tenant.</param><param name="OperationId">Immutable result operation.</param><param name="RepairId">Exact original boundary.</param>
/// <param name="ExpectedRevision">Conditional owner revision.</param><param name="Original">Complete original finite-cohort item.</param>
/// <param name="TerminalState">Exactly Settled, TypedNegative or CancelledBeforeEffect.</param><param name="SourceReceipt">Authoritative original persisted result proof.</param>
public sealed record DirectoryRepairDrainReceipt(string TenantId, string OperationId, string RepairId, long ExpectedRevision,
    DirectoryRepairCohortItem Original, string TerminalState, string SourceReceipt);
