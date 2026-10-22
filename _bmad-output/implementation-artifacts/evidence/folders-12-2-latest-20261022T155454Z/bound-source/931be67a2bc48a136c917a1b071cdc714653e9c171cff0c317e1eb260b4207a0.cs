using System.Runtime.Serialization;

namespace Hexalith.EventStore.Server.Events;

/// <summary>Actor-owned source-v1 evidence over immutable stored event envelopes.</summary>
/// <param name="CodecVersion">The source digest codec version.</param>
/// <param name="TenantId">The exact tenant scope.</param>
/// <param name="Domain">The exact domain scope.</param>
/// <param name="AggregateId">The exact aggregate scope.</param>
/// <param name="SourceIncarnation">The stream incarnation ULID.</param>
/// <param name="HeadSequence">The inclusive aggregate-local head.</param>
/// <param name="HeadMessageId">The persisted head identity, null at zero.</param>
/// <param name="CumulativeDigest">The lowercase hexadecimal source chain digest.</param>
/// <param name="RetainedFloor">The inclusive first retained event sequence.</param>
/// <param name="Erased">Whether the actor has proven completed erasure.</param>
[DataContract]
public sealed record SourceProof(
    [property: DataMember] int CodecVersion,
    [property: DataMember] string TenantId,
    [property: DataMember] string Domain,
    [property: DataMember] string AggregateId,
    [property: DataMember] string SourceIncarnation,
    [property: DataMember] long HeadSequence,
    [property: DataMember] string? HeadMessageId,
    [property: DataMember] string CumulativeDigest,
    [property: DataMember] long RetainedFloor,
    [property: DataMember] bool Erased);
