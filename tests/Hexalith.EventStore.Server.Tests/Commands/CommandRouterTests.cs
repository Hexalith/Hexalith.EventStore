
using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Pipeline.Commands;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Commands;

public class CommandRouterTests {
    private static SubmitCommand CreateTestCommand(
        string tenant = "test-tenant",
        string domain = "test-domain",
        string aggregateId = "agg-001",
        string commandType = "CreateOrder",
        string? correlationId = null) => new(
        MessageId: Guid.NewGuid().ToString(),
        Tenant: tenant,
        Domain: domain,
        AggregateId: aggregateId,
        CommandType: commandType,
        Payload: [1, 2, 3],
        CorrelationId: correlationId ?? Guid.NewGuid().ToString(),
        UserId: "test-user");

    private static (CommandRouter Router, IActorProxyFactory Factory, IAggregateActor ActorProxy) CreateRouter(
        CommandProcessingResult? result = null) {
        IAggregateActor actorProxy = Substitute.For<IAggregateActor>();
        _ = actorProxy.ProcessCommandAsync(Arg.Any<CommandEnvelope>())
            .Returns(result ?? new CommandProcessingResult(true, CorrelationId: "test-correlation"));

        IActorProxyFactory proxyFactory = Substitute.For<IActorProxyFactory>();
        _ = proxyFactory.CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), Arg.Any<string>())
            .Returns(actorProxy);

        var router = new CommandRouter(proxyFactory, NullLogger<CommandRouter>.Instance);
        return (router, proxyFactory, actorProxy);
    }

    [Fact]
    public async Task RouteCommandAsync_ValidCommand_CreatesCorrectActorId() {
        // Arrange
        (CommandRouter router, IActorProxyFactory factory, _) = CreateRouter();
        SubmitCommand command = CreateTestCommand(tenant: "acme", domain: "orders", aggregateId: "order-123");

        // Act
        _ = await router.RouteCommandAsync(command);

        // Assert
        _ = factory.Received(1).CreateActorProxy<IAggregateActor>(
            Arg.Is<ActorId>(id => id.ToString() == "acme:orders:order-123"),
            Arg.Is<string>(s => s == nameof(AggregateActor)));
    }

    [Fact]
    public async Task RouteCommandAsync_ValidCommand_InvokesActorProxy() {
        // Arrange
        (CommandRouter router, _, IAggregateActor actorProxy) = CreateRouter();
        SubmitCommand command = CreateTestCommand();

        // Act
        _ = await router.RouteCommandAsync(command);

        // Assert
        _ = await actorProxy.Received(1).ProcessCommandAsync(Arg.Any<CommandEnvelope>());
    }

    [Fact]
    public async Task RouteCommandAsync_ValidCommand_PassesCommandEnvelope() {
        // Arrange
        (CommandRouter router, _, IAggregateActor actorProxy) = CreateRouter();
        SubmitCommand command = CreateTestCommand(commandType: "PlaceOrder");

        // Act
        _ = await router.RouteCommandAsync(command);

        // Assert
        _ = await actorProxy.Received(1).ProcessCommandAsync(
            Arg.Is<CommandEnvelope>(e =>
                e.TenantId == "test-tenant" &&
                e.Domain == "test-domain" &&
                e.AggregateId == "agg-001" &&
                e.CommandType == "PlaceOrder"));
    }

    [Fact]
    public async Task RouteCommandAsync_ValidCommand_ReturnsActorResult() {
        // Arrange
        var expectedResult = new CommandProcessingResult(true, CorrelationId: "abc-123");
        (CommandRouter router, _, _) = CreateRouter(expectedResult);
        SubmitCommand command = CreateTestCommand();

        // Act
        CommandProcessingResult result = await router.RouteCommandAsync(command);

        // Assert
        result.ShouldBe(expectedResult);
    }

    [Fact]
    public async Task RouteCommandAsync_ActorThrows_PropagatesException() {
        // Arrange
        IAggregateActor actorProxy = Substitute.For<IAggregateActor>();
        _ = actorProxy.ProcessCommandAsync(Arg.Any<CommandEnvelope>())
            .ThrowsAsync(new InvalidOperationException("Actor failure"));

        IActorProxyFactory proxyFactory = Substitute.For<IActorProxyFactory>();
        _ = proxyFactory.CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), Arg.Any<string>())
            .Returns(actorProxy);

        var router = new CommandRouter(proxyFactory, NullLogger<CommandRouter>.Instance);
        SubmitCommand command = CreateTestCommand();

        // Act & Assert
        _ = await Should.ThrowAsync<InvalidOperationException>(
            () => router.RouteCommandAsync(command));
    }

    [Fact]
    public async Task RouteCommandAsync_CommandEnvelope_HasCorrectCorrelationId() {
        // Arrange
        string correlationId = Guid.NewGuid().ToString();
        CommandEnvelope? capturedEnvelope = null;

        IAggregateActor actorProxy = Substitute.For<IAggregateActor>();
        _ = actorProxy.ProcessCommandAsync(Arg.Do<CommandEnvelope>(e => capturedEnvelope = e))
            .Returns(new CommandProcessingResult(true));

        IActorProxyFactory proxyFactory = Substitute.For<IActorProxyFactory>();
        _ = proxyFactory.CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), Arg.Any<string>())
            .Returns(actorProxy);

        var router = new CommandRouter(proxyFactory, NullLogger<CommandRouter>.Instance);
        SubmitCommand command = CreateTestCommand(correlationId: correlationId);

        // Act
        _ = await router.RouteCommandAsync(command);

        // Assert
        _ = capturedEnvelope.ShouldNotBeNull();
        capturedEnvelope.CorrelationId.ShouldBe(correlationId);
    }

    [Fact]
    public async Task RouteCommandAsync_CommandEnvelope_HasCausationIdEqualToMessageId() {
        // Arrange
        CommandEnvelope? capturedEnvelope = null;

        IAggregateActor actorProxy = Substitute.For<IAggregateActor>();
        _ = actorProxy.ProcessCommandAsync(Arg.Do<CommandEnvelope>(e => capturedEnvelope = e))
            .Returns(new CommandProcessingResult(true));

        IActorProxyFactory proxyFactory = Substitute.For<IActorProxyFactory>();
        _ = proxyFactory.CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), Arg.Any<string>())
            .Returns(actorProxy);

        var router = new CommandRouter(proxyFactory, NullLogger<CommandRouter>.Instance);
        SubmitCommand command = CreateTestCommand();

        // Act
        _ = await router.RouteCommandAsync(command);

        // Assert — CausationId is the MessageId of the originating SubmitCommand
        _ = capturedEnvelope.ShouldNotBeNull();
        capturedEnvelope.CausationId.ShouldBe(command.MessageId);
    }

    [Fact]
    public async Task RouteCommandAsync_NullCommand_ThrowsArgumentNullException() {
        // Arrange
        (CommandRouter router, _, _) = CreateRouter();

        // Act & Assert
        _ = await Should.ThrowAsync<ArgumentNullException>(
            () => router.RouteCommandAsync(null!));
    }

    // --- Task 2.1: Multi-tenant actor isolation ---

    [Fact]
    public async Task RouteCommandAsync_DifferentTenants_CreatesDistinctActorIds() {
        // Arrange
        var capturedActorIds = new List<ActorId>();
        IAggregateActor actorProxy = Substitute.For<IAggregateActor>();
        _ = actorProxy.ProcessCommandAsync(Arg.Any<CommandEnvelope>())
            .Returns(new CommandProcessingResult(true));

        IActorProxyFactory proxyFactory = Substitute.For<IActorProxyFactory>();
        _ = proxyFactory.CreateActorProxy<IAggregateActor>(Arg.Do<ActorId>(capturedActorIds.Add), Arg.Any<string>())
            .Returns(actorProxy);

        var router = new CommandRouter(proxyFactory, NullLogger<CommandRouter>.Instance);

        // Act
        _ = await router.RouteCommandAsync(CreateTestCommand(tenant: "tenant-a", domain: "orders", aggregateId: "order-001"));
        _ = await router.RouteCommandAsync(CreateTestCommand(tenant: "tenant-b", domain: "orders", aggregateId: "order-001"));

        // Assert
        capturedActorIds.Count.ShouldBe(2);
        capturedActorIds[0].ToString().ShouldBe("tenant-a:orders:order-001");
        capturedActorIds[1].ToString().ShouldBe("tenant-b:orders:order-001");
        capturedActorIds[0].ShouldNotBe(capturedActorIds[1]);
    }

    [Fact]
    public async Task RouteCommandAsync_DifferentDomains_CreatesDistinctActorIds() {
        // Arrange
        var capturedActorIds = new List<ActorId>();
        IAggregateActor actorProxy = Substitute.For<IAggregateActor>();
        _ = actorProxy.ProcessCommandAsync(Arg.Any<CommandEnvelope>())
            .Returns(new CommandProcessingResult(true));

        IActorProxyFactory proxyFactory = Substitute.For<IActorProxyFactory>();
        _ = proxyFactory.CreateActorProxy<IAggregateActor>(Arg.Do<ActorId>(capturedActorIds.Add), Arg.Any<string>())
            .Returns(actorProxy);

        var router = new CommandRouter(proxyFactory, NullLogger<CommandRouter>.Instance);

        // Act
        _ = await router.RouteCommandAsync(CreateTestCommand(tenant: "tenant-a", domain: "orders", aggregateId: "item-001"));
        _ = await router.RouteCommandAsync(CreateTestCommand(tenant: "tenant-a", domain: "inventory", aggregateId: "item-001"));

        // Assert
        capturedActorIds.Count.ShouldBe(2);
        capturedActorIds[0].ToString().ShouldBe("tenant-a:orders:item-001");
        capturedActorIds[1].ToString().ShouldBe("tenant-a:inventory:item-001");
        capturedActorIds[0].ShouldNotBe(capturedActorIds[1]);
    }

    // --- Negative path: malformed identity propagation ---

    [Theory]
    [InlineData("", "orders", "agg-001")]
    [InlineData("tenant", "", "agg-001")]
    [InlineData("tenant", "orders", "")]
    public async Task RouteCommandAsync_EmptyIdentitySegment_ThrowsArgumentException(
        string tenant, string domain, string aggregateId) {
        // Arrange
        (CommandRouter router, _, _) = CreateRouter();
        SubmitCommand command = CreateTestCommand(tenant: tenant, domain: domain, aggregateId: aggregateId);

        // Act & Assert — AggregateIdentity constructor validates and throws
        _ = await Should.ThrowAsync<ArgumentException>(
            () => router.RouteCommandAsync(command));
    }
}
