using Hexalith.EventStore.Client.Subscriptions;
using Hexalith.EventStore.Client.TestContracts;

namespace Hexalith.EventStore.Client.Tests.Subscriptions;

/// <summary>Captures events from the separate contract fixture assembly.</summary>
internal sealed class ExternalCapturingHandler : IEventStoreDomainEventHandler<ExternalVersionedEvent>
{
    /// <summary>Gets the handled event values.</summary>
    public List<int> Values { get; } = [];

    /// <inheritdoc />
    public Task HandleAsync(ExternalVersionedEvent @event, EventStoreDomainEventContext context,
        CancellationToken cancellationToken = default)
    {
        Values.Add(@event.Value);
        return Task.CompletedTask;
    }
}
