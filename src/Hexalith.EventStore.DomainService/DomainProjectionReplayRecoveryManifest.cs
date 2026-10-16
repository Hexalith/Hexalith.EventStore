using Hexalith.EventStore.Contracts.Projections;

namespace Hexalith.EventStore.DomainService;

/// <summary>One atomic original-manifest value in the existing private replay namespace, never a completion receipt.</summary>
internal sealed record DomainProjectionReplayRecoveryManifest(int Version, string BindingFingerprint,
    string RequestDigest, string Fingerprint, byte[] Manifest, ProjectionOwnerSelection[]? OwnerSelections);
