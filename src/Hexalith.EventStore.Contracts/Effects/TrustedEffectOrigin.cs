namespace Hexalith.EventStore.Contracts.Effects;

/// <summary>Ephemeral target-verified origin conveyed over the authenticated EventStore domain-service channel.</summary>
/// <param name="Identity">The admitted canonical source and target tuple.</param>
/// <param name="Workload">The delegated calling workload.</param>
/// <param name="Purpose">The admitted delegated purpose.</param>
/// <param name="CausationId">The committed source envelope message identifier.</param>
/// <param name="CommandType">The admitted command type.</param>
/// <param name="CommandDigest">SHA-256 of the exact submitted command bytes.</param>
public sealed record TrustedEffectOrigin(EffectIdentity Identity, string Workload, string Purpose,
    string CausationId, string CommandType, string CommandDigest);
