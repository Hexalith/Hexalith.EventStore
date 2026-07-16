using System.Runtime.Serialization;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Identifies a staged projection candidate for promotion or discard.</summary>
/// <param name="OperationId">The stable rebuild operation identity.</param>
[DataContract]
public sealed record ProjectionRebuildCandidateOperation(
    [property: DataMember] string OperationId);
