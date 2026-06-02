using Hexalith.EventStore.Client.Discovery;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Sample.Counter;
using Hexalith.EventStore.Sample.Counter.Events;
using Hexalith.EventStore.Sample.Greeting;

using Shouldly;

namespace Hexalith.EventStore.Sample.Tests;

public class AdminOperationalIndexMetadataTests {
    [Fact]
    public void Create_ReturnsCounterTypesAndProjectionName_WhenCounterRequested() {
        DiscoveryResult discovery = new(
            [
                new DiscoveredDomain(typeof(CounterAggregate), "counter", typeof(Hexalith.EventStore.Sample.Counter.State.CounterState), DomainKind.Aggregate),
                new DiscoveredDomain(typeof(GreetingAggregate), "greeting", typeof(Hexalith.EventStore.Sample.Greeting.State.GreetingState), DomainKind.Aggregate),
            ],
            []);

        AdminOperationalIndexMetadata.Response response = AdminOperationalIndexMetadata.Create(discovery, ["counter"]);

        AdminOperationalIndexMetadata.DomainMetadata counter = response.Domains.Single();
        counter.Domain.ShouldBe("counter");
        counter.AggregateTypes.ShouldContain(typeof(CounterAggregate).FullName!);
        counter.CommandTypes.ShouldContain(t => t.EndsWith(".IncrementCounter", StringComparison.Ordinal));
        counter.EventTypes.ShouldContain(typeof(CounterIncremented).FullName!);
        counter.ProjectionNames.ShouldContain("counter");
    }
}
