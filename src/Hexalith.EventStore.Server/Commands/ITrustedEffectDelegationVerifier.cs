using Hexalith.EventStore.Contracts.Effects;

namespace Hexalith.EventStore.Server.Commands;

/// <summary>Verifies the asymmetric signed workload delegation and attested caller identity.</summary>
public interface ITrustedEffectDelegationVerifier
{
    /// <summary>Rejects unless signature, audience, expiry, issuer, origin, purpose and every effect binding match.</summary>
    Task VerifyAsync(
        TrustedEffectSubmission submission,
        TrustedEffectContext context,
        string semanticDigest,
        CancellationToken cancellationToken = default);
}
