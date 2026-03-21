using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Contracts.Tests.Events;

public class AggregateTerminatedTests {
    [Fact]
    public void AggregateTerminated_ImplementsIRejectionEvent() {
        var terminated = new AggregateTerminated("CounterAggregate", "counter-1");

        _ = Assert.IsAssignableFrom<IRejectionEvent>(terminated);
        _ = Assert.IsAssignableFrom<IEventPayload>(terminated);
    }

    [Fact]
    public void AggregateTerminated_FollowsPastTenseNaming() =>
        Assert.EndsWith("Terminated", nameof(AggregateTerminated));

    [Fact]
    public void AggregateTerminated_StoresProperties() {
        var terminated = new AggregateTerminated("CounterAggregate", "counter-1");

        Assert.Equal("CounterAggregate", terminated.AggregateType);
        Assert.Equal("counter-1", terminated.AggregateId);
    }
}
