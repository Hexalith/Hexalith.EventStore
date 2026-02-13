namespace Hexalith.EventStore.Testing.Tests.Assertions;

using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Testing.Assertions;

public class DomainResultAssertionsTests
{
    [Fact]
    public void ShouldBeSuccess_passes_for_success_result()
    {
        var result = DomainResult.Success(new IEventPayload[] { new TestEvent() });

        DomainResultAssertions.ShouldBeSuccess(result, 1);
    }

    [Fact]
    public void ShouldBeSuccess_fails_for_noop_result()
    {
        var result = DomainResult.NoOp();

        Assert.ThrowsAny<Exception>(() => DomainResultAssertions.ShouldBeSuccess(result, 0));
    }

    [Fact]
    public void ShouldBeRejection_passes_for_rejection_result()
    {
        var result = DomainResult.Rejection(new IRejectionEvent[] { new TestRejection() });

        DomainResultAssertions.ShouldBeRejection(result);
    }

    [Fact]
    public void ShouldBeRejection_fails_for_success_result()
    {
        var result = DomainResult.Success(new IEventPayload[] { new TestEvent() });

        Assert.ThrowsAny<Exception>(() => DomainResultAssertions.ShouldBeRejection(result));
    }

    [Fact]
    public void ShouldBeNoOp_passes_for_noop_result()
    {
        var result = DomainResult.NoOp();

        DomainResultAssertions.ShouldBeNoOp(result);
    }

    [Fact]
    public void ShouldBeNoOp_fails_for_success_result()
    {
        var result = DomainResult.Success(new IEventPayload[] { new TestEvent() });

        Assert.ThrowsAny<Exception>(() => DomainResultAssertions.ShouldBeNoOp(result));
    }

    [Fact]
    public void ShouldContainEvent_passes_when_event_present()
    {
        var result = DomainResult.Success(new IEventPayload[] { new TestEvent() });

        DomainResultAssertions.ShouldContainEvent<TestEvent>(result);
    }

    [Fact]
    public void ShouldContainEvent_fails_when_event_absent()
    {
        var result = DomainResult.Success(new IEventPayload[] { new TestEvent() });

        Assert.ThrowsAny<Exception>(() => DomainResultAssertions.ShouldContainEvent<AnotherEvent>(result));
    }

    private sealed record TestEvent : IEventPayload;

    private sealed record AnotherEvent : IEventPayload;

    private sealed record TestRejection : IRejectionEvent;
}
