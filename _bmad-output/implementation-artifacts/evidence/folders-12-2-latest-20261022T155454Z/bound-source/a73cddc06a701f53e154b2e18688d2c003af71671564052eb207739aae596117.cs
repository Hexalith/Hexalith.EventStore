using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.Server.Security;

/// <summary>Independently authenticated online protection admission; missing authority disables all first effects.</summary>
/// <remarks>Verify the complete JWS against independently published committed tenant/family/version anchors and audience,
/// then perform a linearizable exact guard lookup proving one active attestation and conditional committed dispatch.
/// Signature-only, cached guard reads, human/Workflow/custodian self-approval or open Product dispositions cannot implement this contract.</remarks>
public interface IDeletionConsumptionAuthority : IAnchoredStateTransitionAuthority
{
    /// <summary>Authenticates current private caller/credential for exact tenant/batch-or-revocation identity and named actor method, independently of immutable outcome retention.</summary>
    Task<bool> AuthorizeOperationAsync(string tenantId, string identity, string operation, CancellationToken cancellationToken = default);
    /// <summary>Authenticates the independently installed protection owner and exact tenant/revision/owned safe-state digest, including initial absence. Rollback or divergent restore denies all reads and effects.</summary>
    Task<bool> ValidateStateAsync(string tenantId, long revision, string exactStateDigest, CancellationToken cancellationToken = default);
    /// <summary>Deprecated compatibility-only legacy anchor hook; current recoverable actors do not invoke it.
    /// Qualified implementations must implement the mandatory inherited IAnchoredStateTransitionAuthority admitted-original admission/recovery
    /// and conditional exact transition journal, including independent staging ownership, current permission and final durable-state/anchor confirmation.
    /// Implementing this legacy hook alone never enables an actor; omitted inherited proof defaults deny.</summary>
    Task<bool> RecordRevisionAsync(string tenantId, long expectedRevision, long nextRevision, string exactStateDigest, CancellationToken cancellationToken = default);
    /// <summary>Authenticates exact signed payload/manifest, separate committed issue revision and successful guard dispatch.</summary>
    Task<bool> VerifyDispatchAsync(DeletionBatchConsumptionRequest request, CancellationToken cancellationToken = default);
    /// <summary>Authenticates exact persisted post-seal accepted admission evidence for the specified batch.</summary>
    Task<bool> VerifyAdmissionBlockAsync(DeletionBatchBlockRequest request, CancellationToken cancellationToken = default);
    /// <summary>Authenticates the private current revocation issuer/profile/signature and exact tenant/key event.</summary>
    Task<bool> VerifyRevocationAsync(DeletionCapabilityRevocationEnvelope envelope, CancellationToken cancellationToken = default);
    /// <summary>Verifies exact previous compromise block, same-batch sole-active replacement and successor guard dispatch receipts.</summary>
    Task<bool> VerifyActivationAsync(DeletionReattestationActivation activation, CancellationToken cancellationToken = default);
    /// <summary>Authenticates the exact retained original batch block, canonical signed issued successor/manifest/actual issue receipt and current independently retained successor-key revocation. No dispatch or physical grant is inferred.</summary>
    Task<bool> VerifyBlockedReplacementAsync(DeletionBlockedReplacementReconciliation request, CancellationToken cancellationToken = default)
        => Task.FromResult(false);
}
