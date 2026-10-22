using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.Server.Security;

/// <summary>Injected revocation evidence. Missing or pending evidence denies. The authority does not mint a revocation.</summary>
public interface IPublishedTrustRevocationEvidence
{
    /// <summary>Whether the envelope is the exact installed revocation.</summary>
    /// <param name="envelope">Claimed envelope.</param>
    /// <returns>True only for that exact envelope.</returns>
    bool Matches(DeletionCapabilityRevocationEnvelope envelope);

    /// <summary>Whether the revocation result is still pending.</summary>
    /// <param name="envelope">Claimed envelope.</param>
    /// <returns>True when the result is pending or unknown.</returns>
    bool Pending(DeletionCapabilityRevocationEnvelope envelope);

    /// <summary>Whether the activation cites the exact independently current installed compromise and successor evidence, including its original block-set comparison. This authenticates a retained successor even when its key is now compromised; it grants no consumption permission.</summary>
    /// <param name="activation">Claimed activation.</param>
    /// <returns>True only for that exact activation.</returns>
    bool MatchesActivation(DeletionReattestationActivation activation);

    /// <summary>Whether the activation evidence is still pending.</summary>
    /// <param name="activation">Claimed activation.</param>
    /// <returns>True when the result is pending or unknown.</returns>
    bool PendingActivation(DeletionReattestationActivation activation);
    /// <summary>Whether this exact retained original block, issued successor and independent revocation are installed. Missing evidence denies.</summary>
    /// <param name="request">Captured exact issued-but-blocked reconciliation.</param>
    /// <returns>True only for the complete installed original.</returns>
    bool MatchesBlockedReplacement(DeletionBlockedReplacementReconciliation request) => false;

    /// <summary>Whether exact blocked-successor evidence is pending or unknown.</summary>
    /// <param name="request">Captured exact reconciliation.</param>
    /// <returns>True unless independently known complete.</returns>
    bool PendingBlockedReplacement(DeletionBlockedReplacementReconciliation request) => true;

    /// <summary>Independently verifies current nonrollback guard observation and exact owner key-block-set comparison for this original. Repeated equal reads cannot implement this proof.</summary>
    /// <param name="request">Complete captured original, including the exact successor revocation receipt and block-set revision.</param>
    /// <param name="guardRevision">Fresh detached guard revision.</param>
    /// <param name="guardStateDigest">SHA-256 of the exact detached guard state.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True only for independently authenticated current evidence; absence denies.</returns>
    Task<bool> VerifyCurrentBlockedReplacementAsync(DeletionBlockedReplacementReconciliation request, long guardRevision, string guardStateDigest, CancellationToken cancellationToken = default)
        => Task.FromResult(false);
}
