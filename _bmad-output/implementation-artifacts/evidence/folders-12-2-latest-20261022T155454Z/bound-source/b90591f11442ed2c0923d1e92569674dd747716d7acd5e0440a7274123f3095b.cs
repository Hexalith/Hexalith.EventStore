using Hexalith.EventStore.Contracts.Projections;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>Frozen admission and original owner bindings retained for one route's application and verification.</summary>
internal sealed record ProjectionSourceGuardTailPlan(string Projection, ProjectionDeliveryState State,
    ProjectionDeliveryGuardReservation Binding, ProjectionSourceAdmittedHistory Admitted,
    ProjectionOwnerProof? ObservedOwner)
{
    /// <summary>Gets the complete bounded readable prefix when this route requires canonical staged replay.</summary>
    internal ProjectionEventDto[]? ReplayEvents { get; init; }
}
