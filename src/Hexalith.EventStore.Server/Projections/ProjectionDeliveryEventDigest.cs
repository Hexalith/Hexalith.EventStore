namespace Hexalith.EventStore.Server.Projections;

/// <summary>Contains the support-private digests for one canonical projection event.</summary>
/// <param name="SequenceNumber">The aggregate-local sequence number.</param>
/// <param name="MessageId">The persisted EventStore message identity.</param>
/// <param name="EventFingerprint">The canonical event fingerprint.</param>
/// <param name="PrefixFingerprint">The cumulative prefix fingerprint through this event.</param>
internal sealed record ProjectionDeliveryEventDigest(
    long SequenceNumber,
    string MessageId,
    string EventFingerprint,
    string PrefixFingerprint);
