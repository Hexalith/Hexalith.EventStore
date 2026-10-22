using System.Runtime.Serialization;

namespace Hexalith.EventStore.Contracts.Effects;

/// <summary>Domain-neutral trusted effect request. Its coordinates must be recomputed by the receiving gateway.</summary>
/// <param name="Identity">Immutable source and target coordinates.</param>
/// <param name="CommandType">Target command type.</param>
/// <param name="CommandPayload">Serialized target command.</param>
/// <param name="MessageId">Expected deterministic message identifier.</param>
/// <param name="IdempotencyKey">Expected deterministic idempotency key.</param>
[DataContract]
public sealed record TrustedEffectSubmission(
    [property: DataMember] EffectIdentity Identity,
    [property: DataMember] string CommandType,
    [property: DataMember] byte[] CommandPayload,
    [property: DataMember] string MessageId,
    [property: DataMember] string IdempotencyKey);
