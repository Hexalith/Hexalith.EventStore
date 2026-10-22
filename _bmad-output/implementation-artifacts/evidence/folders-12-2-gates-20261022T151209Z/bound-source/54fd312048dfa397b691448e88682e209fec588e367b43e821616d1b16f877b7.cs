using Hexalith.EventStore.Contracts.Commands;

namespace Hexalith.EventStore.Contracts.Tests.Commands;

public class ICommandContractTests
{
    [Fact]
    public void ICommandContract_StaticMembers_AreAccessible()
    {
        CreateCounter.CommandType.ShouldBe("create-counter");
        CreateCounter.Domain.ShouldBe("counter");
    }

    [Fact]
    public void ICommandContract_StaticMembers_AreAccessibleThroughGenericConstraint()
    {
        GetCommandType<CreateCounter>().ShouldBe("create-counter");
        GetDomain<CreateCounter>().ShouldBe("counter");
    }

    [Fact]
    public void ICommandContract_InstanceAggregateId_IsAccessible()
    {
        var command = new CreateCounter("counter-123");

        command.AggregateId.ShouldBe("counter-123");
    }

    [Fact]
    public void ICommandContract_AggregateId_CanProjectFromDomainProperty()
    {
        ICommandContract command = new CreateTenant("tenant-abc");

        command.AggregateId.ShouldBe("tenant-abc");
    }

    [Fact]
    public void ICommandContract_DistinctTypes_HaveDistinctMetadata()
    {
        CreateCounter.CommandType.ShouldNotBe(CreateTenant.CommandType);
        CreateCounter.Domain.ShouldNotBe(CreateTenant.Domain);
    }

    private static string GetCommandType<TCommand>()
        where TCommand : ICommandContract
        => TCommand.CommandType;

    private static string GetDomain<TCommand>()
        where TCommand : ICommandContract
        => TCommand.Domain;
}
