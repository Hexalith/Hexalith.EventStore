using Hexalith.EventStore.Contracts.Commands;

namespace Hexalith.EventStore.Contracts.Tests.Commands;

// --- Test stub types implementing ICommandContract ---

internal sealed record CreateCounter(string AggregateId) : ICommandContract {
    public static string CommandType => "create-counter";

    public static string Domain => "counter";
}

internal sealed record CreateTenant(string TenantId) : ICommandContract {
    public static string CommandType => "create-tenant";

    public static string Domain => "tenants";

    public string AggregateId => TenantId;
}

public class ICommandContractTests {
    [Fact]
    public void ICommandContract_StaticMembers_AreAccessible() {
        CreateCounter.CommandType.ShouldBe("create-counter");
        CreateCounter.Domain.ShouldBe("counter");
    }

    [Fact]
    public void ICommandContract_InstanceAggregateId_IsAccessible() {
        var command = new CreateCounter("counter-123");

        command.AggregateId.ShouldBe("counter-123");
    }

    [Fact]
    public void ICommandContract_AggregateId_CanProjectFromDomainProperty() {
        ICommandContract command = new CreateTenant("tenant-abc");

        command.AggregateId.ShouldBe("tenant-abc");
    }

    [Fact]
    public void ICommandContract_DistinctTypes_HaveDistinctMetadata() {
        CreateCounter.CommandType.ShouldNotBe(CreateTenant.CommandType);
        CreateCounter.Domain.ShouldNotBe(CreateTenant.Domain);
    }
}
