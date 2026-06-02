using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Client.Subscriptions;

/// <summary>
/// Handles a specific domain event type in a service that consumes a domain's published events
/// (a downstream/read-side consumer subscribing via DAPR pub/sub).
/// </summary>
/// <remarks>
/// This is the platform generalization of the per-domain consumer handler interfaces domain modules
/// previously hand-wrote (e.g. <c>ITenantEventHandler{TEvent}</c>). Register implementations with
/// <c>AddEventStoreDomainEventHandler{TEvent, THandler}</c> and consume the published stream with
/// <c>AddEventStoreDomainEvents</c> + <c>MapEventStoreDomainEvents</c>.
/// </remarks>
/// <typeparam name="TEvent">The event payload type.</typeparam>
public interface IEventStoreDomainEventHandler<in TEvent>
    where TEvent : IEventPayload {
    /// <summary>
    /// Handles the specified domain event asynchronously.
    /// </summary>
    /// <param name="event">The event payload.</param>
    /// <param name="context">The event processing context carrying envelope metadata.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task HandleAsync(TEvent @event, EventStoreDomainEventContext context, CancellationToken cancellationToken = default);
}
