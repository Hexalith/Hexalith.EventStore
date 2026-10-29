using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.Server.Security;

/// <summary>Fresh asynchronous independently authenticated evidence for production registrars; no cached synchronous decision substitutes for these reads.</summary>
public interface IAsyncPublishedTrustRevocationEvidence : IPublishedTrustRevocationEvidence
{
    /// <summary>Authenticates the exact current original revocation and denies pending evidence.</summary>
    Task<bool> VerifyRevocationEvidenceAsync(DeletionCapabilityRevocationEnvelope envelope, CancellationToken cancellationToken);
    /// <summary>Authenticates exact current original compromise, replacement and dispatch evidence.</summary>
    Task<bool> VerifyActivationEvidenceAsync(DeletionReattestationActivation activation, CancellationToken cancellationToken);
    /// <summary>Authenticates exact current original issued-but-blocked replacement evidence.</summary>
    Task<bool> VerifyBlockedReplacementEvidenceAsync(DeletionBlockedReplacementReconciliation request, CancellationToken cancellationToken);
}
