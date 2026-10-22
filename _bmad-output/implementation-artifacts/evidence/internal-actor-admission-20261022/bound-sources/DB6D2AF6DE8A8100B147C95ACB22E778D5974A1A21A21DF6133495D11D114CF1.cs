using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.EventStore.Contracts.Security;

/// <summary>Current actor-free exact-source proof of terminal destruction for an expired attribution transition.</summary>
/// <param name="Identity">Original source identity.</param>
/// <param name="Purpose">Exact retained-history purpose.</param>
/// <param name="PolicyId">Original accepted retention policy of the destroyed custody unit.</param>
/// <param name="SourceSequence">Original committed transition position.</param>
/// <param name="EventTypeName">Existing public persisted contract identity; no decrypted payload or actor is retained.</param>
/// <param name="SealedPayloadDigest">SHA-256 of original ciphertext bytes, never plaintext or an actor identifier.</param>
/// <param name="DestructionReceiptId">Opaque authenticated all-copy terminal destruction receipt.</param>
/// <param name="LifecycleRevision">Current nonrollback terminal lifecycle revision.</param>
/// <param name="AuthorityRevision">Current independent certificate authority.</param>
/// <param name="ObservedAt">Fresh certificate observation.</param>
/// <param name="ValidUntil">Exclusive current certificate authority limit; it never renews a binding.</param>
/// <param name="ContractVersion">Closed certificate schema.</param>
/// <remarks>Contains no expired actor, effective interval, binding payload, login, logical intent or decrypted predecessor.
/// It supports version continuity only; it cannot identify an expired past action or grant current authority.</remarks>
public sealed record ExpiredIdentityHistoryCertificate(AggregateIdentity Identity, string Purpose, string PolicyId, long SourceSequence,
    string EventTypeName, string SealedPayloadDigest, string DestructionReceiptId, long LifecycleRevision,
    string AuthorityRevision, DateTimeOffset ObservedAt, DateTimeOffset ValidUntil, int ContractVersion = 1);
