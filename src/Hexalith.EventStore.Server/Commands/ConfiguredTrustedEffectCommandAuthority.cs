using Hexalith.EventStore.Contracts.Effects;

using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Server.Commands;

/// <summary>Exact match, deny by default trusted effect command authority.</summary>
public sealed class ConfiguredTrustedEffectCommandAuthority(IOptions<TrustedEffectAuthorityOptions> options)
    : ITrustedEffectCommandAuthority
{
    /// <inheritdoc/>
    public bool IsAllowed(TrustedEffectSubmission submission, TrustedEffectContext context)
        => options.Value.Rules.Any(rule =>
            string.Equals(rule.Workload, context.Workload, StringComparison.Ordinal)
            && string.Equals(rule.Purpose, context.Purpose, StringComparison.Ordinal)
            && string.Equals(rule.TargetDomain, submission.Identity.TargetDomain, StringComparison.Ordinal)
            && string.Equals(rule.CommandType, submission.CommandType, StringComparison.Ordinal));
}
