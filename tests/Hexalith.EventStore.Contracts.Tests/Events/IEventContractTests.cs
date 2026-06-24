using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Contracts.Tests.Events;

internal sealed record CounterCreated(string AggregateId) : IEventContract {
    public static string EventType => "counter-created";

    public static string Domain => "counter";
}

internal sealed record TenantCreated(string TenantId) : IEventContract {
    public static string EventType => "tenant-created";

    public static string Domain => "tenants";

    public string AggregateId => TenantId;
}

public class IEventContractTests {
    [Fact]
    public void IEventContract_StaticMembers_AreAccessible() {
        CounterCreated.EventType.ShouldBe("counter-created");
        CounterCreated.Domain.ShouldBe("counter");
    }

    [Fact]
    public void IEventContract_InstanceAggregateId_IsAccessible() {
        var @event = new CounterCreated("counter-123");

        @event.AggregateId.ShouldBe("counter-123");
    }

    [Fact]
    public void IEventContract_AggregateId_CanProjectFromDomainProperty() {
        IEventContract @event = new TenantCreated("tenant-abc");

        @event.AggregateId.ShouldBe("tenant-abc");
    }

    [Fact]
    public void IEventContract_DistinctTypes_HaveDistinctMetadata() {
        CounterCreated.EventType.ShouldNotBe(TenantCreated.EventType);
        CounterCreated.Domain.ShouldNotBe(TenantCreated.Domain);
    }
}
