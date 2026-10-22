using System.Runtime.Serialization;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Identifies one delivery write that owns a persisted lifecycle lease.</summary>
/// <param name="OperationId">The stable delivery identity held for the complete write.</param>
/// <param name="LeaseExpiresAtUtc">The bounded instant after which a different durable delivery may reclaim the scope.</param>
[DataContract]
public sealed record ProjectionDeliveryLifecycleRequest(
    [property: DataMember] string OperationId,
    [property: DataMember] DateTimeOffset? LeaseExpiresAtUtc = null);
