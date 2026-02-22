namespace Hexalith.EventStore.Contracts.Tests.Results;

using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Results;

public class DomainResultTests {
    private sealed class TestEvent : IEventPayload;

    private sealed class TestRejection : IRejectionEvent;

    [Fact]
    public void Success_WithEvents_IsSuccessTrue() {
        var events = new IEventPayload[] { new TestEvent(), new TestEvent() };
        DomainResult result = DomainResult.Success(events);

        Assert.True(result.IsSuccess);
        Assert.False(result.IsRejection);
        Assert.False(result.IsNoOp);
        Assert.Equal(2, result.Events.Count);
    }

    [Fact]
    public void Rejection_WithRejectionEvents_IsRejectionTrue() {
        var events = new IRejectionEvent[] { new TestRejection() };
        DomainResult result = DomainResult.Rejection(events);

        Assert.False(result.IsSuccess);
        Assert.True(result.IsRejection);
        Assert.False(result.IsNoOp);
        Assert.Single(result.Events);
    }

    [Fact]
    public void NoOp_ReturnsEmptyResult() {
        DomainResult result = DomainResult.NoOp();

        Assert.False(result.IsSuccess);
        Assert.False(result.IsRejection);
        Assert.True(result.IsNoOp);
        Assert.Empty(result.Events);
    }

    [Fact]
    public void Constructor_WithMixedEvents_ThrowsArgumentException() {
        var events = new IEventPayload[] { new TestEvent(), new TestRejection() };

        Assert.Throws<ArgumentException>(() => new DomainResult(events));
    }

    [Fact]
    public void Events_ReturnsImmutableList() {
        var events = new IEventPayload[] { new TestEvent() };
        DomainResult result = DomainResult.Success(events);

        Assert.IsAssignableFrom<IReadOnlyList<IEventPayload>>(result.Events);
    }

    [Fact]
    public void Success_WithEmptyList_ThrowsArgumentException() {
        Assert.Throws<ArgumentException>(() => DomainResult.Success(Array.Empty<IEventPayload>()));
    }

    [Fact]
    public void Rejection_WithEmptyList_ThrowsArgumentException() {
        Assert.Throws<ArgumentException>(() => DomainResult.Rejection(Array.Empty<IRejectionEvent>()));
    }

    [Fact]
    public void Constructor_WithNullEvents_ThrowsArgumentNullException() {
        Assert.Throws<ArgumentNullException>(() => new DomainResult(null!));
    }

    [Fact]
    public void Success_WithMultipleRegularEvents_AllAccessible() {
        var event1 = new TestEvent();
        var event2 = new TestEvent();
        DomainResult result = DomainResult.Success(new IEventPayload[] { event1, event2 });

        Assert.Same(event1, result.Events[0]);
        Assert.Same(event2, result.Events[1]);
    }

    [Fact]
    public void Rejection_WithMultipleRejections_AllAccessible() {
        var rej1 = new TestRejection();
        var rej2 = new TestRejection();
        DomainResult result = DomainResult.Rejection(new IRejectionEvent[] { rej1, rej2 });

        Assert.Equal(2, result.Events.Count);
        Assert.Same(rej1, result.Events[0]);
        Assert.Same(rej2, result.Events[1]);
    }

    [Fact]
    public void Constructor_WithOnlyRejectionEvents_IsRejectionTrue() {
        var events = new IEventPayload[] { new TestRejection(), new TestRejection() };
        var result = new DomainResult(events);

        Assert.True(result.IsRejection);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Constructor_WithOnlyRegularEvents_IsSuccessTrue() {
        var events = new IEventPayload[] { new TestEvent() };
        var result = new DomainResult(events);

        Assert.True(result.IsSuccess);
        Assert.False(result.IsRejection);
    }

    [Fact]
    public void Constructor_WithEmptyEvents_IsNoOpTrue() {
        var result = new DomainResult(Array.Empty<IEventPayload>());

        Assert.True(result.IsNoOp);
    }
}
