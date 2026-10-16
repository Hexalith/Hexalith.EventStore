using System.Runtime.Serialization;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Read-only recovery evidence from the actor that owns rebuild publication.</summary>
/// <param name="OperationId">The requested original operation.</param>
/// <param name="Status">The persisted recovery disposition.</param>
/// <param name="State">Only the matching operation's complete candidate or selected state.</param>
[DataContract]
public sealed record ProjectionRebuildInspection(
    [property: DataMember] string OperationId,
    [property: DataMember] ProjectionRebuildInspectionStatus Status,
    [property: DataMember] ProjectionState? State);
