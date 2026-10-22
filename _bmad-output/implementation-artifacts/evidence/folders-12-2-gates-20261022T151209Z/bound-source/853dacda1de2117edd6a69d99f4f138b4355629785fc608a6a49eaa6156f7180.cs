using System.Runtime.Serialization;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Bounded source ordering retained in the existing publication receipt across replay modes.</summary>
/// <param name="SourceActorId">The exact tenant/domain/aggregate source scope.</param>
/// <param name="ProjectionName">The selected projection route.</param>
/// <param name="OperationId">The operation that selected this complete prefix.</param>
/// <param name="HeadSequence">The inclusive selected source sequence; this is not a delivery checkpoint.</param>
[DataContract]
public sealed record ProjectionReplaySelectionFence(
    [property: DataMember] string SourceActorId,
    [property: DataMember] string ProjectionName,
    [property: DataMember] string OperationId,
    [property: DataMember] long HeadSequence);
