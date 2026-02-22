
using System.Reflection;

using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Contracts.Tests.Events;

public class EventPayloadTests {
    private sealed class TestEventPayload : IEventPayload;

    private sealed class TestRejectionEvent : IRejectionEvent;

    [Fact]
    public void IEventPayload_IsMarkerInterface_WithNoMembers() {
        MemberInfo[] members = typeof(IEventPayload).GetMembers();
        Assert.Empty(members);
    }

    [Fact]
    public void IRejectionEvent_ExtendsIEventPayload() => Assert.True(typeof(IEventPayload).IsAssignableFrom(typeof(IRejectionEvent)));

    [Fact]
    public void IRejectionEvent_HasNoAdditionalMembers() {
        MemberInfo[] members = typeof(IRejectionEvent).GetMembers();
        Assert.Empty(members);
    }

    [Fact]
    public void RejectionEvent_ImplementsBothInterfaces() {
        var rejection = new TestRejectionEvent();

        _ = Assert.IsAssignableFrom<IRejectionEvent>(rejection);
        _ = Assert.IsAssignableFrom<IEventPayload>(rejection);
    }

    [Fact]
    public void EventPayload_ImplementsInterface() {
        var payload = new TestEventPayload();

        _ = Assert.IsAssignableFrom<IEventPayload>(payload);
    }
}
