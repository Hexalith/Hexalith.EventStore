namespace Hexalith.EventStore.Contracts.Security;

/// <summary>Independent exact source/authority facts for one transition and target mutation digest. Missing production issuer, source provenance, restore proof or Product disposition yields no evidence.</summary>
/// <param name="TenantId">Exact TenantId.</param>
/// <param name="IntentDigest">Exact IntentDigest.</param>
/// <param name="TargetMutationDigest">Exact TargetMutationDigest.</param>
/// <param name="AuthorityRevision">Exact AuthorityRevision.</param>
/// <param name="AuthorityReceiptId">Exact AuthorityReceiptId.</param>
/// <param name="ObservedAt">Exact ObservedAt.</param>
/// <param name="ValidUntil">Exact ValidUntil.</param>
/// <param name="RequiredOwnerIds">Exact RequiredOwnerIds.</param>
/// <param name="ObligationIds">Exact ObligationIds.</param>
/// <param name="RepairCohort">Exact RepairCohort.</param>
/// <param name="WriterEnforcementReceipt">Exact WriterEnforcementReceipt.</param>
/// <param name="LegacyRevocationReceipt">Exact LegacyRevocationReceipt.</param>
/// <param name="CurrentZeroReceiptId">Exact CurrentZeroReceiptId.</param>
/// <param name="ZeroOrdinal">Exact ZeroOrdinal.</param>
/// <param name="Disposition">Exact Disposition.</param>
/// <param name="DispositionVersion">Exact DispositionVersion.</param>
/// <param name="RequiredOutcomeReceiptIds">Exact RequiredOutcomeReceiptIds.</param>
/// <param name="ProtectionBlockReceiptId">Exact ProtectionBlockReceiptId.</param>
public sealed record GovernanceGuardEvidence(string TenantId, string IntentDigest, string TargetMutationDigest, string AuthorityRevision, string AuthorityReceiptId, DateTimeOffset ObservedAt, DateTimeOffset ValidUntil, IReadOnlyList<string> RequiredOwnerIds, IReadOnlyList<string> ObligationIds, IReadOnlyList<DirectoryRepairCohortItem> RepairCohort, string WriterEnforcementReceipt, string LegacyRevocationReceipt, string CurrentZeroReceiptId, long ZeroOrdinal, string Disposition, string DispositionVersion, IReadOnlyList<string> RequiredOutcomeReceiptIds, string ProtectionBlockReceiptId)
{
    /// <summary>Independent concrete resource provenance for the exact append and supplied target mutation vector; absence denies a fresh append.</summary>
    public string AppendResourceId { get; init; } = "";
    /// <summary>Independently authenticated committed operation owning the violating resource and immutable source facts.</summary>
    public string ViolationAcceptanceOperationId { get; init; } = "";
    /// <summary>Independent exact original target mutation vector digest for the violating resource.</summary>
    public string ViolationTargetMutationDigest { get; init; } = "";
    /// <summary>Original authenticated accepted write receipt, including its own append-time ordinal and high water.</summary>
    public GovernanceProtocolReceipt? OriginalAcceptance { get; init; }
    /// <summary>Original first-event/permit facts for the exact violating resource.</summary>
    public GovernanceWriteFacts? ViolationWriteFacts { get; init; }
    /// <summary>Authoritative original resource identity; it is not recovered from caller classification.</summary>
    public string ViolationResourceId { get; init; } = "";
    /// <summary>Complete exact violating target; required for post-seal content containment.</summary>
    public GovernanceProtectionTarget? ViolationProtectionTarget { get; init; }
    /// <summary>Independently authenticated installed deletion-capability issuer.</summary>
    public string CapabilityIssuer { get; init; } = "";
    /// <summary>Independently authenticated exact protection-owner audience.</summary>
    public string CapabilityAudience { get; init; } = "";
    /// <summary>Installed exact private tenant guard stream identity.</summary>
    public string GuardStreamId { get; init; } = "";
    /// <summary>Exact independently authenticated no-global-cut/candidate/token receipt for the first tokenless violation.</summary>
    public string NoCutReceiptId { get; init; } = "";
    /// <summary>Independently authenticated complete original protection-owner block, including its immutable affected-batch snapshot.</summary>
    public DeletionCapabilityRevocationReceipt? RevocationReceipt { get; init; }
    /// <summary>Independently authenticated original protection request, retaining exact issued attestation and dispatch identity for irreversible reservation recovery.</summary>
    public DeletionBatchConsumptionRequest? ProtectionOriginalRequest { get; init; }
    /// <summary>Fresh independently authenticated exact terminal outcome for that original request; omitted affected-batch membership is never reservation proof.</summary>
    public DeletionConsumptionOutcome? ProtectionTerminalOutcome { get; init; }

    /// <summary>Independently authenticated exact activation request including its refreshed global comparison and immutable original block receipt.</summary>
    public DeletionReattestationActivation? ProtectionActivationRequest { get; init; }
    /// <summary>Independently authenticated original activation outcome at the exact request comparison; no absent revocation mirror is inferred.</summary>
    public DeletionConsumptionOutcome? ProtectionActivationOutcome { get; init; }
    /// <summary>Independently authenticated exact retained no-dispatch blocked replacement and original owner outcome.</summary>
    public DeletionBlockedReplacementResult? ProtectionBlockedReplacement { get; init; }
}
