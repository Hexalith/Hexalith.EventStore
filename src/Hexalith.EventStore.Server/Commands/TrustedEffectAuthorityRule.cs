namespace Hexalith.EventStore.Server.Commands;

/// <summary>One explicit workload, purpose, domain, and command permission.</summary>
/// <param name="Workload">Attested calling workload.</param>
/// <param name="Purpose">Signed delegated purpose.</param>
/// <param name="TargetDomain">Command target domain.</param>
/// <param name="CommandType">Command type.</param>
public sealed record TrustedEffectAuthorityRule(
    string Workload,
    string Purpose,
    string TargetDomain,
    string CommandType);
