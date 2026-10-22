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

    /// <summary>Whether the activation cites the exact installed compromise and successor evidence.</summary>
    /// <param name="activation">Claimed activation.</param>
    /// <returns>True only for that exact activation.</returns>
    bool MatchesActivation(DeletionReattestationActivation activation);

    /// <summary>Whether the activation evidence is still pending.</summary>
    /// <param name="activation">Claimed activation.</param>
    /// <returns>True when the result is pending or unknown.</returns>
    bool PendingActivation(DeletionReattestationActivation activation);
}
