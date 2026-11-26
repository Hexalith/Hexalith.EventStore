namespace Hexalith.EventStore.Client.Subscriptions;

/// <summary>Identifies one fenced attempt for a content-bound subscription marker.</summary>
/// <param name="MessageId">The persisted event identifier.</param>
/// <param name="Fingerprint">The fingerprint of the admitted event envelope.</param>
/// <param name="Fence">The attempt identifier assigned by the marker store.</param>
public sealed record EventStoreDomainEventMarkerLease(string MessageId, string Fingerprint, Guid Fence);
