using System.Runtime.Serialization;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Operation-scoped projection state that is invisible until explicit promotion.</summary>
/// <param name="OperationId">The stable rebuild operation identity.</param>
/// <param name="State">The complete-prefix projection candidate.</param>
[DataContract]
public sealed record ProjectionRebuildCandidate(
    [property: DataMember] string OperationId,
    [property: DataMember] ProjectionState State);
