namespace Hexalith.EventStore.Contracts.Security;

/// <summary>Exact independently proved issued-but-blocked successor. This phase grants no dispatch, reservation, signing or physical consumption authority.</summary>
/// <param name="OperationId">Stable complete original reconciliation identity.</param><param name="CompromiseBlockReceiptId">Exact retained previous batch block.</param>
/// <param name="ExpectedKeyBlockSetRevision">Current independent block-set comparison.</param><param name="GuardReplacementReceiptId">Exact committed replacement issue receipt.</param>
/// <param name="Capability">Exact canonical issued successor.</param><param name="DetachedJws">Exact retained signed artifact.</param>
/// <param name="SigningRequestId">Canonical original signing request.</param><param name="CommittedIssuedGuardRevision">Actual durable issue revision.</param>
/// <param name="Targets">Unchanged exact immutable manifest.</param><param name="RevocationReceipt">Exact independently retained successor-key revocation.</param>
public sealed record DeletionBlockedReplacementReconciliation(string OperationId, string CompromiseBlockReceiptId, long ExpectedKeyBlockSetRevision,
    string GuardReplacementReceiptId, DeletionBatchCapabilityV1 Capability, string DetachedJws, string SigningRequestId,
    long CommittedIssuedGuardRevision, IReadOnlyList<ProtectionTarget> Targets, DeletionCapabilityRevocationReceipt RevocationReceipt);

/// <summary>Only an independently retained original blocked disposition; never a dispatch or physical effect receipt.</summary>
/// <param name="Original">Exact original no-effect reconciliation.</param><param name="Outcome">Its original immutable owner outcome.</param>
public sealed record DeletionBlockedReplacementResult(DeletionBlockedReplacementReconciliation Original, DeletionConsumptionOutcome Outcome);
