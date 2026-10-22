using Hexalith.EventStore.Contracts.Effects;

namespace Hexalith.EventStore.Server.Commands;

/// <summary>Checks an explicit workload and purpose allow-list for a target command.</summary>
public interface ITrustedEffectCommandAuthority
{
    /// <summary>Returns true only for an explicitly registered origin, purpose, domain, and command type.</summary>
    bool IsAllowed(TrustedEffectSubmission submission, TrustedEffectContext context);
}
