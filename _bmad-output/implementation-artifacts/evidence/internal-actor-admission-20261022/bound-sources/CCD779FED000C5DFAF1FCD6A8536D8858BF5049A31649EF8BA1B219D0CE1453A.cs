using System.Runtime.Serialization;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>A publication call fenced by the actual lifecycle snapshot sampled by its owner.</summary>
/// <param name="Binding">The immutable original-work binding.</param>
/// <param name="Lifecycle">The actual admitted delivery snapshot; revisions are never predicted.</param>
/// <param name="LifecycleOperationId">The unique invocation's owned delivery lease token.</param>
/// <param name="State">The complete candidate, supplied only for initial staging.</param>
[DataContract]
public sealed record ProjectionReplayPublicationRequest(
    [property: DataMember] ProjectionReplayPublicationBinding Binding,
    [property: DataMember] ProjectionLifecycleSnapshot Lifecycle,
    [property: DataMember] string LifecycleOperationId,
    [property: DataMember] ProjectionState? State);
