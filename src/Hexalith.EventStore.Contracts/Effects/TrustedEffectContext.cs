using System.Runtime.Serialization;

namespace Hexalith.EventStore.Contracts.Effects;

/// <summary>Authenticated provenance supplied alongside a trusted submission.</summary>
/// <param name="Workload">Attested calling workload.</param>
/// <param name="Purpose">Named delegated purpose.</param>
/// <param name="CausationId">Source event causation identifier.</param>
/// <param name="DelegationToken">Short-lived, signed workload delegation credential.</param>
[DataContract]
public sealed record TrustedEffectContext(
    [property: DataMember] string Workload,
    [property: DataMember] string Purpose,
    [property: DataMember] string CausationId,
    [property: DataMember] string DelegationToken);
