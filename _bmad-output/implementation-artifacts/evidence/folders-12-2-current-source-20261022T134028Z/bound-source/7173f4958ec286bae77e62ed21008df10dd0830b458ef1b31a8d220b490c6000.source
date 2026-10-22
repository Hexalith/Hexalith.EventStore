namespace Hexalith.EventStore.Contracts.Security;

/// <summary>Candidate private installed all-writer epoch metadata; strings alone do not prove enforcement.</summary>
/// <param name="TenantId">Exact tenant.</param><param name="OperationId">Immutable installation operation.</param>
/// <param name="EpochId">Installed epoch.</param><param name="ExpectedRevision">Conditional owner revision.</param>
/// <param name="LegacyRevocationReceipt">Independent exact legacy-writer revocation.</param><param name="WriterEnforcementReceipt">Qualified all-writer enforcement proof.</param>
public sealed record DirectoryEpochInstallation(string TenantId, string OperationId, string EpochId, long ExpectedRevision,
    string LegacyRevocationReceipt, string WriterEnforcementReceipt);
