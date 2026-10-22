using System.Runtime.Serialization;

namespace Hexalith.EventStore.Contracts.Effects;

/// <summary>Authoritative disposition backed by the target actor receipt.</summary>
/// <param name="EffectId">Deterministic effect identifier.</param>
/// <param name="Disposition">Recorded target outcome.</param>
/// <param name="Replayed">Whether the target returned a prior receipt without handling again.</param>
/// <param name="ResultPayload">Optional domain result.</param>
[DataContract]
public sealed record TrustedEffectResult(
    [property: DataMember] string EffectId,
    [property: DataMember] TrustedEffectDisposition Disposition,
    [property: DataMember] bool Replayed,
    [property: DataMember] string? ResultPayload);
