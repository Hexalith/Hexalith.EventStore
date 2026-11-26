namespace Hexalith.EventStore.Client.Subscriptions;

/// <summary>Reports marker admission and the lease for an owner or proven dispatch.</summary>
/// <param name="Result">The marker admission result.</param>
/// <param name="Lease">The lease, when the caller may advance the marker.</param>
public sealed record EventStoreDomainEventMarkerAdmission(
    EventStoreDomainEventMarkerAcquisitionResult Result,
    EventStoreDomainEventMarkerLease? Lease = null);
