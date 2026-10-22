namespace Hexalith.EventStore.Contracts.Security;

/// <summary>Candidate private exact repair checkpoint returned by a qualified atomic all-writer fence/cohort owner; never an observed maximum.</summary>
/// <param name="TenantId">Exact tenant.</param><param name="OperationId">Stable repair identity.</param><param name="EpochId">Fenced original epoch.</param>
/// <param name="ExpectedRevision">Conditional boundary-owner revision.</param><param name="NamespaceCheckpoint">Authenticated complete post-fence finite namespace checkpoint.</param>
/// <param name="WriterRevocationReceipt">Exact atomic repair write-revocation receipt.</param><param name="Cohort">Sorted distinct finite original owner operations.</param>
public sealed record DirectoryRepairBoundary(string TenantId, string OperationId, string EpochId, long ExpectedRevision, string NamespaceCheckpoint,
    string WriterRevocationReceipt, IReadOnlyList<DirectoryRepairCohortItem> Cohort);
