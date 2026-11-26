using Hexalith.EventStore.Client.Subscriptions;

namespace Hexalith.EventStore.Operations.Models;

/// <summary>Operations request for a single admitted subscription.</summary>
/// <param name="Delivery">The admitted delivery.</param>
/// <param name="Failure">The optional bounded failure to persist.</param>
/// <param name="ObservedAtUtc">Time of observation at the Operations boundary.</param>
public sealed record SubscriberDeliveryObservation(
    EventStoreSubscriberDelivery Delivery,
    EventStoreSubscriberFailure? Failure,
    DateTimeOffset ObservedAtUtc);
