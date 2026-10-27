using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Events;

namespace PublishedConsumer;

/// <summary>Ends the fixture's publication boundary after real event persistence without external broker side effects.</summary>
public sealed class BoundedEventPublisher : IEventPublisher
{
    public Task<EventPublishResult> PublishEventsAsync(AggregateIdentity identity, IReadOnlyList<EventEnvelope> events,
        string correlationId, CancellationToken cancellationToken = default, bool triggerProjectionUpdate = true)
        => Task.FromResult(new EventPublishResult(true, events.Count, null));
}
