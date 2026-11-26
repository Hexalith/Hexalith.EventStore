namespace Hexalith.EventStore.Client.Subscriptions;

/// <summary>Operations-owned durable evidence for one admitted subscriber delivery.</summary>
public interface IEventStoreSubscriberPoisonSink
{
    /// <summary>Confirms retained state, or durably records a failure before reporting exhaustion.</summary>
    /// <param name="delivery">The validated content and subscription scope.</param>
    /// <param name="failure">The latest failure, or null to check existing retained evidence.</param>
    /// <param name="cancellationToken">The operation cancellation token.</param>
    /// <returns>The durable capture outcome.</returns>
    Task<EventStoreSubscriberPoisonOutcome> ObserveAsync(EventStoreSubscriberDelivery delivery,
        EventStoreSubscriberFailure? failure, CancellationToken cancellationToken = default);
}
