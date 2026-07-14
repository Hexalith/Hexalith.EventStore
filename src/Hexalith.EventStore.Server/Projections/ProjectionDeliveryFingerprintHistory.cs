namespace Hexalith.EventStore.Server.Projections;

/// <summary>Contains a canonical projection history fingerprint and its event digests.</summary>
/// <param name="InitialFingerprint">The scope-separated empty-prefix fingerprint.</param>
/// <param name="PrefixFingerprint">The cumulative fingerprint through the final event.</param>
/// <param name="Events">The ordered canonical event digests.</param>
internal sealed record ProjectionDeliveryFingerprintHistory(
    string InitialFingerprint,
    string PrefixFingerprint,
    IReadOnlyList<ProjectionDeliveryEventDigest> Events);
