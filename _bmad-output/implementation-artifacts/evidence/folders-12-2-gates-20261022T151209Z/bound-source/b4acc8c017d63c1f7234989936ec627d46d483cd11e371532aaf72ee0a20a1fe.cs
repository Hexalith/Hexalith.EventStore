namespace Hexalith.EventStore.Contracts.Events;

/// <summary>Contains raw event bytes and sidecar evidence returned by an authenticated provider source.</summary>
/// <param name="StorageKey">The durable provider key for the event.</param>
/// <param name="SequenceNumber">The event sequence number.</param>
/// <param name="RawEnvelope">The exact immutable raw envelope bytes.</param>
/// <param name="EncodingEvidence">The immutable provider encoding evidence, when present.</param>
/// <param name="StoredDigestEvidence">The immutable stored digest evidence, when present.</param>
/// <param name="V1OriginEvidence">The immutable retained V1 origin evidence, when present.</param>
/// <param name="ActorIntentCertificate">The immutable actor intent certificate, when present.</param>
/// <param name="ActorCommitReceipt">The immutable committed provider receipt, when present.</param>
/// <remarks>Returned values are untrusted until their page proof and cross hashes are authenticated.</remarks>
[Obsolete("Legacy event evolution compatibility contract.")]
public sealed record AuthenticatedRawEvent(
    string StorageKey,
    long SequenceNumber,
    IReadOnlyPayload RawEnvelope,
    IReadOnlyPayload? EncodingEvidence,
    IReadOnlyPayload? StoredDigestEvidence,
    IReadOnlyPayload? V1OriginEvidence,
    IReadOnlyPayload? ActorIntentCertificate,
    IReadOnlyPayload? ActorCommitReceipt);
