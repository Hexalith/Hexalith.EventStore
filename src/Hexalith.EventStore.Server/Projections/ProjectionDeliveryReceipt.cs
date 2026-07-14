namespace Hexalith.EventStore.Server.Projections;

/// <summary>Persisted exact-completion evidence for one projection event.</summary>
/// <param name="SequenceNumber">The aggregate-local event sequence.</param>
/// <param name="MessageId">The persisted EventStore message identity.</param>
/// <param name="EventFingerprint">The canonical event fingerprint.</param>
/// <param name="PrefixFingerprint">The canonical cumulative prefix through this event.</param>
internal sealed record ProjectionDeliveryReceipt(
    long SequenceNumber,
    string MessageId,
    string EventFingerprint,
    string PrefixFingerprint);
