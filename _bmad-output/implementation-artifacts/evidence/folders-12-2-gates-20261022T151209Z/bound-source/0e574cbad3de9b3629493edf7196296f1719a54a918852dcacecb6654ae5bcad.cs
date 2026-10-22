using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Contracts.Tests.Events;

public class AggregateTerminatedTests {
    [Fact]
    public void AggregateTerminated_ImplementsIRejectionEvent() {
        var terminated = new AggregateTerminated("CounterAggregate", "counter-1");

        _ = terminated.ShouldBeAssignableTo<IRejectionEvent>();
        _ = terminated.ShouldBeAssignableTo<IEventPayload>();
    }

    [Fact]
    public void AggregateTerminated_FollowsPastTenseNaming() =>
        nameof(AggregateTerminated).ShouldEndWith("Terminated");

    [Fact]
    public void AggregateTerminated_StoresProperties() {
        var terminated = new AggregateTerminated("CounterAggregate", "counter-1");

        terminated.AggregateType.ShouldBe("CounterAggregate");
        terminated.AggregateId.ShouldBe("counter-1");
    }
}
