using Dapr.Actors;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Additive read-only validation of the exact current delivery lease.</summary>
public interface IProjectionDeliveryLeaseActor : IActor
{
    /// <summary>Validates operation ownership, unexpired lease and actual revision without renewal.</summary>
    Task<bool> OwnsDeliveryLeaseAsync(ProjectionDeliveryLeaseProof request);
}
