using System.Runtime.Serialization;

namespace Hexalith.EventStore.Contracts.Effects;

/// <summary>Immutable source and target coordinates for one logical effect.</summary>
/// <param name="Tenant">Canonical tenant identifier.</param>
/// <param name="SourceDomain">Source stream domain.</param>
/// <param name="SourceAggregate">Source stream aggregate.</param>
/// <param name="SourceEnvelopeSequence">Source EventStore envelope sequence.</param>
/// <param name="EffectKind">Versioned effect kind.</param>
/// <param name="TargetDomain">Target stream domain.</param>
/// <param name="TargetAggregate">Target stream aggregate.</param>
/// <param name="Ordinal">Catalog ordinal for the source event, kind, and target role.</param>
[DataContract]
public sealed record EffectIdentity(
    [property: DataMember] string Tenant,
    [property: DataMember] string SourceDomain,
    [property: DataMember] string SourceAggregate,
    [property: DataMember] long SourceEnvelopeSequence,
    [property: DataMember] string EffectKind,
    [property: DataMember] string TargetDomain,
    [property: DataMember] string TargetAggregate,
    [property: DataMember] long Ordinal);
