using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Server.Actors;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>Reads owning SDK completion proof and rechecks the supplied lifecycle snapshot.</summary>
internal interface IProjectionSelectedOwnerProofGateway
{
    /// <summary>Reads at most two owner metadata keys and one lifecycle snapshot, without repair or handler calls.</summary>
    Task<ProjectionOwnerProofResult> ReadAsync(AggregateIdentity identity, NamedProjectionRouteCatalogEntry catalog,
        string projectionName, ProjectionLifecycleSnapshot snapshot, CancellationToken cancellationToken);
}
