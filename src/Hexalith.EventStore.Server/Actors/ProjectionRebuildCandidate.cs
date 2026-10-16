using System.Runtime.Serialization;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Operation-scoped projection state that is invisible until explicit promotion.</summary>
/// <param name="OperationId">The stable rebuild operation identity.</param>
/// <param name="State">The complete-prefix projection candidate.</param>
[DataContract]
public sealed record ProjectionRebuildCandidate(
    [property: DataMember] string OperationId,
    [property: DataMember] ProjectionState State)
{
    /// <summary>Gets the optional immutable live-fallback publication binding.</summary>
    [DataMember]
    public ProjectionReplayPublicationBinding? PublicationBinding { get; init; }

    /// <summary>Gets the retained prior completed publication floor during an ordinary rebuild.</summary>
    [DataMember]
    public ProjectionReplayPublicationBinding? PublicationFloor { get; init; }

    /// <summary>Gets the selected source ordering supplied by the owning replay orchestrator.</summary>
    [DataMember]
    public ProjectionReplaySelectionFence? SelectedReplayFence { get; init; }

    /// <summary>Gets the prior selected ordering retained while this candidate remains non-live.</summary>
    [DataMember]
    public ProjectionReplaySelectionFence? PriorReplayFence { get; init; }
}
