namespace Hexalith.EventStore.Server.Projections;

/// <summary>Returns one projection route's pre-handler delivery decision.</summary>
/// <param name="Disposition">The handler admission disposition.</param>
/// <param name="ReasonCode">The optional bounded support reason.</param>
/// <param name="Reservation">The acquired fenced reservation, when dispatch is admitted.</param>
internal sealed record ProjectionDeliveryAdmissionResult(
    ProjectionDeliveryAdmissionDisposition Disposition,
    string? ReasonCode,
    ProjectionDeliveryReservation? Reservation);
