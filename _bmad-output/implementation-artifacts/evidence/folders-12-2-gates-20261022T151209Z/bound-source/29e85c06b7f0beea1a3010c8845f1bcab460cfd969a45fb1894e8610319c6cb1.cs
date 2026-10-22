using System.Runtime.Serialization;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Read-only evidence request for one exact delivery lease and lifecycle revision.</summary>
/// <param name="OperationId">The unique invocation's owned lifecycle operation.</param>
/// <param name="Snapshot">The actual delivery phase and revision sampled by that owner.</param>
[DataContract]
public sealed record ProjectionDeliveryLeaseProof(
    [property: DataMember] string OperationId,
    [property: DataMember] ProjectionLifecycleSnapshot Snapshot);
