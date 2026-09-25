using Hexalith.EventStore.Contracts.Effects;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Private target-partition receipt committed with a domain outcome.</summary>
/// <param name="EffectId">Deterministic identity.</param>
/// <param name="Identity">Full immutable source and target tuple.</param>
/// <param name="SemanticDigest">Server-derived canonical command digest.</param>
/// <param name="Disposition">Target outcome.</param>
/// <param name="Workload">Authenticated workload.</param>
/// <param name="Purpose">Signed delegated purpose.</param>
/// <param name="CausationId">Source event causation.</param>
/// <param name="ResultPayload">Optional domain result.</param>
internal sealed record EffectReceipt(
    string EffectId,
    EffectIdentity Identity,
    string SemanticDigest,
    TrustedEffectDisposition Disposition,
    string Workload,
    string Purpose,
    string CausationId,
    string? ResultPayload);
