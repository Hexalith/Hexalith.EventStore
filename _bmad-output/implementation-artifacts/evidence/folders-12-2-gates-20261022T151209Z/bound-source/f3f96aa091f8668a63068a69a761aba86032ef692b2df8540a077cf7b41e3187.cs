using System.Runtime.Serialization;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Immutable original-work references for compatible complete-prefix publication.</summary>
/// <param name="TenantId">The source tenant.</param>
/// <param name="Domain">The source domain.</param>
/// <param name="AggregateId">The source aggregate.</param>
/// <param name="ProjectionName">The discovered stable projection type.</param>
/// <param name="OperationId">The durable original candidate identity.</param>
/// <param name="HeadSequence">The inclusive admitted head.</param>
/// <param name="HeadMessageId">The original EventStore head identity.</param>
/// <param name="SourceFingerprint">The complete stored-source fingerprint.</param>
/// <param name="CandidateFingerprint">The exact complete candidate digest.</param>
[DataContract]
public sealed record ProjectionReplayPublicationBinding(
    [property: DataMember] string TenantId,
    [property: DataMember] string Domain,
    [property: DataMember] string AggregateId,
    [property: DataMember] string ProjectionName,
    [property: DataMember] string OperationId,
    [property: DataMember] long HeadSequence,
    [property: DataMember] string HeadMessageId,
    [property: DataMember] string SourceFingerprint,
    [property: DataMember] string CandidateFingerprint);
