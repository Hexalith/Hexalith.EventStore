namespace Hexalith.EventStore.Server.Projections;

/// <summary>Persisted fenced reservation for one projection delivery suffix.</summary>
/// <param name="StartSequence">The first sequence admitted for the handler.</param>
/// <param name="EndSequence">The admitted stream head.</param>
/// <param name="HeadMessageId">The persisted identity at the admitted head.</param>
/// <param name="DispatchId">The stable v2 dispatch and batch identity.</param>
/// <param name="ManifestFingerprint">The admitted event manifest fingerprint.</param>
/// <param name="FencingToken">The monotonically increasing completion fence.</param>
/// <param name="AdmittedAt">The UTC reservation time.</param>
/// <param name="ExpiresAt">The UTC lease expiry.</param>
/// <param name="Attempt">The reservation attempt.</param>
internal sealed record ProjectionDeliveryReservation(
    long StartSequence,
    long EndSequence,
    string HeadMessageId,
    string DispatchId,
    string ManifestFingerprint,
    long FencingToken,
    DateTimeOffset AdmittedAt,
    DateTimeOffset ExpiresAt,
    int Attempt);
