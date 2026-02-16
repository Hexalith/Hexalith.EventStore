namespace Hexalith.EventStore.Server.Tests.Commands;

using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Tests.Fixtures;
using Hexalith.EventStore.Testing.Builders;

using Shouldly;

/// <summary>
/// Story 7.4 / AC #2: Command routing integration tests.
/// Validates command routing to correct aggregate actors via Dapr.
/// </summary>
[Collection("DaprTestContainer")]
public class CommandRoutingIntegrationTests
{
    private readonly DaprTestContainerFixture _fixture;

    public CommandRoutingIntegrationTests(DaprTestContainerFixture fixture)
    {
        _fixture = fixture;
        _fixture.SetupCounterDomain();
    }

    /// <summary>
    /// Task 2.2: Different domains route to different actor instances.
    /// </summary>
    [Fact]
    public async Task ProcessCommandAsync_DifferentDomains_RouteToDifferentActors()
    {
        // Arrange
        var actorProxyFactory = new ActorProxyFactory(new ActorProxyOptions
        {
            HttpEndpoint = _fixture.DaprHttpEndpoint,
        });

        var commandCounter = new CommandEnvelopeBuilder()
            .WithTenantId("tenant-a")
            .WithDomain("counter")
            .WithAggregateId("domain-routing-test")
            .WithCommandType("IncrementCounter")
            .Build();

        // Another domain with the same aggregate ID
        var commandOther = new CommandEnvelopeBuilder()
            .WithTenantId("tenant-a")
            .WithDomain("inventory")
            .WithAggregateId("domain-routing-test")
            .WithCommandType("IncrementCounter")
            .Build();

        IAggregateActor proxyCounter = actorProxyFactory.CreateActorProxy<IAggregateActor>(
            new ActorId(commandCounter.AggregateIdentity.ActorId),
            nameof(AggregateActor));

        IAggregateActor proxyOther = actorProxyFactory.CreateActorProxy<IAggregateActor>(
            new ActorId(commandOther.AggregateIdentity.ActorId),
            nameof(AggregateActor));

        // Act
        CommandProcessingResult resultCounter = await proxyCounter.ProcessCommandAsync(commandCounter);
        CommandProcessingResult resultOther = await proxyOther.ProcessCommandAsync(commandOther);

        // Assert - both succeed on different actors
        resultCounter.Accepted.ShouldBeTrue();
        resultOther.Accepted.ShouldBeTrue();
        commandCounter.AggregateIdentity.ActorId.ShouldNotBe(commandOther.AggregateIdentity.ActorId);
    }

    /// <summary>
    /// Task 2.2: NoOp domain result returns accepted with zero events.
    /// </summary>
    [Fact]
    public async Task ProcessCommandAsync_NoOpResult_ReturnsAcceptedWithZeroEvents()
    {
        // Arrange
        _fixture.DomainServiceInvoker.SetupResponse("NoOpCommand", Hexalith.EventStore.Contracts.Results.DomainResult.NoOp());

        var actorProxyFactory = new ActorProxyFactory(new ActorProxyOptions
        {
            HttpEndpoint = _fixture.DaprHttpEndpoint,
        });

        string aggregateId = $"noop-test-{Guid.NewGuid():N}";
        var command = new CommandEnvelopeBuilder()
            .WithTenantId("tenant-a")
            .WithDomain("counter")
            .WithAggregateId(aggregateId)
            .WithCommandType("NoOpCommand")
            .Build();

        IAggregateActor proxy = actorProxyFactory.CreateActorProxy<IAggregateActor>(
            new ActorId(command.AggregateIdentity.ActorId),
            nameof(AggregateActor));

        // Act
        CommandProcessingResult result = await proxy.ProcessCommandAsync(command);

        // Assert
        result.Accepted.ShouldBeTrue("NoOp should be treated as accepted");
        result.EventCount.ShouldBe(0, "NoOp should produce zero events");
    }
}
