namespace Hexalith.EventStore.Contracts.Security;

/// <summary>Immutable accepted or singleton containment batch, current sole attestation, dispatch and exact all-target outcome.</summary>
/// <param name="BatchId">Exact BatchId.</param>
/// <param name="Kind">Exact Kind.</param>
/// <param name="Ordinal">Exact Ordinal.</param>
/// <param name="Targets">Exact Targets.</param>
/// <param name="ManifestDigest">Exact ManifestDigest.</param>
/// <param name="AttestationOrdinal">Exact AttestationOrdinal.</param>
/// <param name="CapabilityKeyVersion">Exact CapabilityKeyVersion.</param>
/// <param name="AttestationDigest">Exact AttestationDigest.</param>
/// <param name="IssueReceiptId">Exact IssueReceiptId.</param>
/// <param name="DispatchReceiptId">Exact DispatchReceiptId.</param>
/// <param name="ProtectionOutcome">Exact ProtectionOutcome.</param>
/// <param name="ProtectionReceiptId">Exact ProtectionReceiptId.</param>
/// <param name="TargetReceiptIds">Exact TargetReceiptIds.</param>
/// <param name="BlockSetRevision">Exact BlockSetRevision.</param>
/// <param name="CoveredResourceIds">Exact authenticated post-seal content resources covered by this immutable target manifest.</param>
public sealed record GovernanceBatchState(string BatchId, string Kind, long Ordinal, IReadOnlyList<GovernanceProtectionTarget> Targets, string ManifestDigest, long AttestationOrdinal, string CapabilityKeyVersion, string AttestationDigest, string IssueReceiptId, string DispatchReceiptId, string ProtectionOutcome, string ProtectionReceiptId, IReadOnlyList<string> TargetReceiptIds, long BlockSetRevision, IReadOnlyList<string> CoveredResourceIds)
{
    /// <summary>Exact issued fourteen-field original or sole active replacement credential.</summary>
    public DeletionBatchCapabilityV1? Capability { get; init; }
    /// <summary>Original retained detached signature artifact.</summary>
    public string DetachedJws { get; init; } = "";
    /// <summary>Original deterministic signing request identity.</summary>
    public string SigningRequestId { get; init; } = "";
    /// <summary>Actual successful issue revision, retained separately from the signed intended revision.</summary>
    public long IssuedGuardRevision { get; init; }
    /// <summary>Actual successful exact active-attestation dispatch revision.</summary>
    public long DispatchGuardRevision { get; init; }
}
