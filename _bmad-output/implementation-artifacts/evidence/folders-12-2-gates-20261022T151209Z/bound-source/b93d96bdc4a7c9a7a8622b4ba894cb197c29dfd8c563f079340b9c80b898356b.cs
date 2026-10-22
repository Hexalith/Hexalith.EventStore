using System.Reflection;

using Hexalith.EventStore.Client.Commands;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Rest;
using Hexalith.EventStore.Sample.Counter.Commands;

using Shouldly;

namespace Hexalith.EventStore.Sample.Tests.Counter.Commands;

public sealed class CounterCommandContractTests
{
    [Fact]
    public void IncrementCounter_Contract_IsValid()
        => AssertContract<IncrementCounter>("increment-counter", "{counterId}/increment");

    [Fact]
    public void DecrementCounter_Contract_IsValid()
        => AssertContract<DecrementCounter>("decrement-counter", "{counterId}/decrement");

    [Fact]
    public void ResetCounter_Contract_IsValid()
        => AssertContract<ResetCounter>("reset-counter", "{counterId}/reset");

    [Fact]
    public void CloseCounter_Contract_IsValid()
        => AssertContract<CloseCounter>("close-counter", "{counterId}/close");

    [Fact]
    public void Commands_DefaultAggregateId_IsCounter1()
    {
        new IncrementCounter().AggregateId.ShouldBe("counter-1");
        new DecrementCounter().AggregateId.ShouldBe("counter-1");
        new ResetCounter().AggregateId.ShouldBe("counter-1");
        new CloseCounter().AggregateId.ShouldBe("counter-1");
    }

    [Fact]
    public void Commands_AggregateId_ReflectsCounterId()
    {
        new IncrementCounter("counter-42").AggregateId.ShouldBe("counter-42");
        new DecrementCounter("counter-42").AggregateId.ShouldBe("counter-42");
        new ResetCounter("counter-42").AggregateId.ShouldBe("counter-42");
        new CloseCounter("counter-42").AggregateId.ShouldBe("counter-42");
    }

    private static void AssertContract<TCommand>(string expectedCommandType, string expectedTemplate)
        where TCommand : ICommandContract
    {
        TCommand.Domain.ShouldBe("counter");
        TCommand.CommandType.ShouldBe(expectedCommandType);

        // Kebab-case, colon-free discriminator per the D1 ICommandContract contract.
        expectedCommandType.ShouldBe(expectedCommandType.ToLowerInvariant());
        expectedCommandType.ShouldNotContain(":");
        expectedCommandType.ShouldNotContain(" ");

        RestRouteAttribute? route = typeof(TCommand).GetCustomAttribute<RestRouteAttribute>();
        route.ShouldNotBeNull();
        route.Verb.ShouldBe(RestVerb.Post);
        route.Template.ShouldBe(expectedTemplate);
        route.ApiScope.ShouldBe("counter");

        // The platform resolver reads and validates the same static metadata the generator emits.
        CommandContractMetadata metadata = CommandContractResolver.Resolve<TCommand>();
        metadata.CommandType.ShouldBe(expectedCommandType);
        metadata.Domain.ShouldBe("counter");
    }
}
