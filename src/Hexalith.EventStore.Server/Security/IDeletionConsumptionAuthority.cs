using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.Server.Security;

/// <summary>Independently authenticated online protection admission; missing authority disables all first effects.</summary>
/// <remarks>Verify the complete JWS against independently published committed tenant/family/version anchors and audience,
/// then perform a linearizable exact guard lookup proving one active attestation and conditional committed dispatch.
/// Signature-only, cached guard reads, human/Workflow/custodian self-approval or open Product dispositions cannot implement this contract.</remarks>
public interface IDeletionConsumptionAuthority
{
    /// <summary>Authenticates current private caller/credential for exact tenant/batch-or-revocation identity and named actor method, independently of immutable outcome retention.</summary>
    Task<bool> AuthorizeOperationAsync(string tenantId, string identity, string operation, CancellationToken cancellationToken = default);
    /// <summary>Authenticates the independently installed protection owner and exact tenant/revision/owned safe-state digest, including initial absence. Rollback or divergent restore denies all reads and effects.</summary>
    Task<bool> ValidateStateAsync(string tenantId, long revision, string exactStateDigest, CancellationToken cancellationToken = default);
    /// <summary>Conditionally advances the independently durable installed-owner anchor before ledger persistence. A failed or unknown store save leaves availability closed until exact independent reconciliation; no anchor rollback is permitted.</summary>
    Task<bool> RecordRevisionAsync(string tenantId, long expectedRevision, long nextRevision, string exactStateDigest, CancellationToken cancellationToken = default);
    /// <summary>Authenticates exact signed payload/manifest, separate committed issue revision and successful guard dispatch.</summary>
    Task<bool> VerifyDispatchAsync(DeletionBatchConsumptionRequest request, CancellationToken cancellationToken = default);
    /// <summary>Authenticates exact persisted post-seal accepted admission evidence for the specified batch.</summary>
    Task<bool> VerifyAdmissionBlockAsync(DeletionBatchBlockRequest request, CancellationToken cancellationToken = default);
    /// <summary>Authenticates the private current revocation issuer/profile/signature and exact tenant/key event.</summary>
    Task<bool> VerifyRevocationAsync(DeletionCapabilityRevocationEnvelope envelope, CancellationToken cancellationToken = default);
    /// <summary>Verifies exact previous compromise block, same-batch sole-active replacement and successor guard dispatch receipts.</summary>
    Task<bool> VerifyActivationAsync(DeletionReattestationActivation activation, CancellationToken cancellationToken = default);
}
