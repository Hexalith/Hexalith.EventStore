using System.Runtime.Serialization;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Identifies one idempotent projection rebuild lifecycle operation.</summary>
/// <param name="OperationId">The stable rebuild operation identity.</param>
[DataContract]
public sealed record ProjectionRebuildLifecycleRequest(
    [property: DataMember] string OperationId);
