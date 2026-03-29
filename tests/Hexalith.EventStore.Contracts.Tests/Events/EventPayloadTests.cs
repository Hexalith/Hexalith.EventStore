
using System.Reflection;

using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Contracts.Tests.Events;

public class EventPayloadTests {
    private sealed class TestEventPayload : IEventPayload;

    private sealed class TestRejectionEvent : IRejectionEvent;

    [Fact]
    public void IEventPayload_IsMarkerInterface_WithNoMembers() {
        MemberInfo[] members = typeof(IEventPayload).GetMembers();
        members.ShouldBeEmpty();
    }

    [Fact]
    public void IRejectionEvent_ExtendsIEventPayload() => typeof(IEventPayload).IsAssignableFrom(typeof(IRejectionEvent)).ShouldBeTrue();

    [Fact]
    public void IRejectionEvent_HasNoAdditionalMembers() {
        MemberInfo[] members = typeof(IRejectionEvent).GetMembers();
        members.ShouldBeEmpty();
    }

    [Fact]
    public void RejectionEvent_ImplementsBothInterfaces() {
        var rejection = new TestRejectionEvent();

        _ = rejection.ShouldBeAssignableTo<IRejectionEvent>();
        _ = rejection.ShouldBeAssignableTo<IEventPayload>();
    }

    [Fact]
    public void EventPayload_ImplementsInterface() {
        var payload = new TestEventPayload();

        _ = payload.ShouldBeAssignableTo<IEventPayload>();
    }
}
