
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Results;

namespace Hexalith.EventStore.Contracts.Tests.Results;

public class DomainResultTests {
    private sealed class TestEvent : IEventPayload;

    private sealed class TestRejection : IRejectionEvent;

    [Fact]
    public void Success_WithEvents_IsSuccessTrue() {
        var events = new IEventPayload[] { new TestEvent(), new TestEvent() };
        var result = DomainResult.Success(events);

        result.IsSuccess.ShouldBeTrue();
        result.IsRejection.ShouldBeFalse();
        result.IsNoOp.ShouldBeFalse();
        result.Events.Count.ShouldBe(2);
    }

    [Fact]
    public void Rejection_WithRejectionEvents_IsRejectionTrue() {
        var events = new IRejectionEvent[] { new TestRejection() };
        var result = DomainResult.Rejection(events);

        result.IsSuccess.ShouldBeFalse();
        result.IsRejection.ShouldBeTrue();
        result.IsNoOp.ShouldBeFalse();
        _ = result.Events.ShouldHaveSingleItem();
    }

    [Fact]
    public void NoOp_ReturnsEmptyResult() {
        var result = DomainResult.NoOp();

        result.IsSuccess.ShouldBeFalse();
        result.IsRejection.ShouldBeFalse();
        result.IsNoOp.ShouldBeTrue();
        result.Events.ShouldBeEmpty();
    }

    [Fact]
    public void Constructor_WithMixedEvents_ThrowsArgumentException() {
        var events = new IEventPayload[] { new TestEvent(), new TestRejection() };

        _ = Should.Throw<ArgumentException>(() => new DomainResult(events));
    }

    [Fact]
    public void Events_ReturnsImmutableList() {
        var events = new IEventPayload[] { new TestEvent() };
        var result = DomainResult.Success(events);

        _ = result.Events.ShouldBeAssignableTo<IReadOnlyList<IEventPayload>>();
    }

    [Fact]
    public void Success_WithEmptyList_ThrowsArgumentException() => Should.Throw<ArgumentException>(() => DomainResult.Success(Array.Empty<IEventPayload>()));

    [Fact]
    public void Rejection_WithEmptyList_ThrowsArgumentException() => Should.Throw<ArgumentException>(() => DomainResult.Rejection(Array.Empty<IRejectionEvent>()));

    [Fact]
    public void Constructor_WithNullEvents_ThrowsArgumentNullException() => Should.Throw<ArgumentNullException>(() => new DomainResult(null!));

    [Fact]
    public void Success_WithMultipleRegularEvents_AllAccessible() {
        var event1 = new TestEvent();
        var event2 = new TestEvent();
        var result = DomainResult.Success(new IEventPayload[] { event1, event2 });

        result.Events[0].ShouldBeSameAs(event1);
        result.Events[1].ShouldBeSameAs(event2);
    }

    [Fact]
    public void Rejection_WithMultipleRejections_AllAccessible() {
        var rej1 = new TestRejection();
        var rej2 = new TestRejection();
        var result = DomainResult.Rejection(new IRejectionEvent[] { rej1, rej2 });

        result.Events.Count.ShouldBe(2);
        result.Events[0].ShouldBeSameAs(rej1);
        result.Events[1].ShouldBeSameAs(rej2);
    }

    [Fact]
    public void Constructor_WithOnlyRejectionEvents_IsRejectionTrue() {
        var events = new IEventPayload[] { new TestRejection(), new TestRejection() };
        var result = new DomainResult(events);

        result.IsRejection.ShouldBeTrue();
        result.IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public void Constructor_WithOnlyRegularEvents_IsSuccessTrue() {
        var events = new IEventPayload[] { new TestEvent() };
        var result = new DomainResult(events);

        result.IsSuccess.ShouldBeTrue();
        result.IsRejection.ShouldBeFalse();
    }

    [Fact]
    public void Constructor_WithEmptyEvents_IsNoOpTrue() {
        var result = new DomainResult(Array.Empty<IEventPayload>());

        result.IsNoOp.ShouldBeTrue();
    }
}
