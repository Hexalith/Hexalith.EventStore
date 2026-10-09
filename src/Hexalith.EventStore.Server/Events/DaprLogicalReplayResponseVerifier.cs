using Hexalith.EventStore.Client.Aggregates;
using Hexalith.EventStore.Client.Events;

namespace Hexalith.EventStore.Server.Events;

/// <summary>Shares the Client logical response verifier for retained operation participant admission.</summary>
internal static class DaprLogicalReplayResponseVerifier
{
    /// <summary>Verifies exact signed logical framing under current source, trust and predecessor.</summary>
    internal static DaprLogicalVerifiedClaim<DaprLogicalPrefixClaim> Verify(ReadOnlySpan<byte> response,
        DaprLogicalSourceBinding binding, DaprLogicalClaimTrust trust, ReadOnlyMemory<byte> previousAccumulator,
        EventBufferBudget budget, CancellationToken cancellationToken, DaprLogicalAnchoredIntake? anchored = null)
        => PrivateLogicalReplayResponseVerifier.Verify(response, binding, trust, previousAccumulator, budget, cancellationToken, anchored);
}
