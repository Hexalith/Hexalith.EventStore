using Hexalith.EventStore.Contracts.Projections;

namespace Hexalith.EventStore.DomainService;

/// <summary>Bounded immutable references to one original guarded rebuild manifest in the existing transport namespace.</summary>
/// <param name="Version">The private manifest reference version.</param>
/// <param name="RequestDigest">The exact admitted request identity.</param>
/// <param name="Fingerprint">The original coordinated batch fingerprint.</param>
/// <param name="Owners">The selections already bound by the canonical manifest operations.</param>
internal sealed record DomainProjectionReplayManifest(int Version, string RequestDigest, string Fingerprint,
    ProjectionOwnerSelection[] Owners);
