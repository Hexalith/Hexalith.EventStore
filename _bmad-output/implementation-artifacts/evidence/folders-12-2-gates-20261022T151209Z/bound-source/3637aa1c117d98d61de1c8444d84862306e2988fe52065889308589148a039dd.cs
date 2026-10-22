using Hexalith.EventStore.Contracts.Effects;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Private durable quarantine evidence for an admitted colliding effect.</summary>
/// <param name="EffectId">Colliding effect identifier.</param>
/// <param name="OriginalSemanticDigest">Digest already bound to the target outcome.</param>
/// <param name="AttemptedIdentity">Attempted full identity tuple.</param>
/// <param name="AttemptedSemanticDigest">Attempted server-derived digest.</param>
/// <param name="Workload">Authenticated workload.</param>
/// <param name="Purpose">Delegated purpose.</param>
/// <param name="CausationId">Source causation.</param>
/// <param name="ObservedAt">First quarantine observation.</param>
internal sealed record EffectCollisionRecord(
    string EffectId,
    string OriginalSemanticDigest,
    EffectIdentity AttemptedIdentity,
    string AttemptedSemanticDigest,
    string Workload,
    string Purpose,
    string CausationId,
    DateTimeOffset ObservedAt);
