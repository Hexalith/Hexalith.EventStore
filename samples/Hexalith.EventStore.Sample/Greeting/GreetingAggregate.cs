
using Hexalith.EventStore.Client.Aggregates;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Sample.Greeting.Commands;
using Hexalith.EventStore.Sample.Greeting.Events;
using Hexalith.EventStore.Sample.Greeting.State;

namespace Hexalith.EventStore.Sample.Greeting;

/// <summary>Minimal domain demonstrating multi-domain registration. See CounterAggregate for full pattern repertoire (rejection, no-op, tombstoning).</summary>
public sealed class GreetingAggregate : EventStoreAggregate<GreetingState> {
    public static DomainResult Handle(SendGreeting command, GreetingState? state)
        => DomainResult.Success(new IEventPayload[] { new GreetingSent() });
}
