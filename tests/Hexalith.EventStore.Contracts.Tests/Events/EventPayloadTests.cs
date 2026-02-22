namespace Hexalith.EventStore.Contracts.Tests.Events;

using Hexalith.EventStore.Contracts.Events;

public class EventPayloadTests {
    private sealed class TestEventPayload : IEventPayload;

    private sealed class TestRejectionEvent : IRejectionEvent;

    [Fact]
    public void IEventPayload_IsMarkerInterface_WithNoMembers() {
        var members = typeof(IEventPayload).GetMembers();
        Assert.Empty(members);
    }

    [Fact]
    public void IRejectionEvent_ExtendsIEventPayload() {
        Assert.True(typeof(IEventPayload).IsAssignableFrom(typeof(IRejectionEvent)));
    }

    [Fact]
    public void IRejectionEvent_HasNoAdditionalMembers() {
        var members = typeof(IRejectionEvent).GetMembers();
        Assert.Empty(members);
    }

    [Fact]
    public void RejectionEvent_ImplementsBothInterfaces() {
        var rejection = new TestRejectionEvent();

        Assert.IsAssignableFrom<IRejectionEvent>(rejection);
        Assert.IsAssignableFrom<IEventPayload>(rejection);
    }

    [Fact]
    public void EventPayload_ImplementsInterface() {
        var payload = new TestEventPayload();

        Assert.IsAssignableFrom<IEventPayload>(payload);
    }
}
