using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Server.Actors;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>One validated catalog route observation used to plan a frozen suffix before effects.</summary>
internal sealed record ProjectionSourceGuardRouteObservation(string Projection, ProjectionDeliveryState State,
    ProjectionLifecycleSnapshot Lifecycle, ProjectionOwnerProofResult Selected);
