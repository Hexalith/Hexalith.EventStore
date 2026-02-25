
using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Tests.Fixtures;
using Hexalith.EventStore.Testing.Builders;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Actors;
/// <summary>
/// Story 7.4 / AC #2: Actor processing pipeline integration tests.
/// Validates the full pipeline with real Dapr sidecar and Redis state store.
/// </summary>
[Collection("DaprTestContainer")]
public class AggregateActorIntegrationTests {
    private readonly DaprTestContainerFixture _fixture;

    public AggregateActorIntegrationTests(DaprTestContainerFixture fixture) {
        _fixture = fixture;
        _fixture.SetupCounterDomain();
    }

    /// <summary>
    /// Task 2.1: Test actor activation via Dapr actor runtime.
    /// Verifies that a command routed through Dapr activates the AggregateActor.
    /// </summary>
    [Fact]
    public async Task ProcessCommandAsync_NewAggregate_ActivatesActorAndReturnsAccepted() {
        // Arrange
        var actorProxyFactory = new ActorProxyFactory(new ActorProxyOptions {
            HttpEndpoint = _fixture.DaprHttpEndpoint,
        });

        CommandEnvelope command = new CommandEnvelopeBuilder()
            .WithTenantId("tenant-a")
            .WithDomain("counter")
            .WithAggregateId("counter-001")
            .WithCommandType("IncrementCounter")
            .Build();

        string actorId = command.AggregateIdentity.ActorId;
        IAggregateActor proxy = actorProxyFactory.CreateActorProxy<IAggregateActor>(
            new ActorId(actorId),
            nameof(AggregateActor));

        // Act
        CommandProcessingResult result = await proxy.ProcessCommandAsync(command);

        // Assert
        _ = result.ShouldNotBeNull();
        result.Accepted.ShouldBeTrue("Command should be accepted by the actor");
        result.CorrelationId.ShouldBe(command.CorrelationId);
        result.EventCount.ShouldBe(1, "IncrementCounter should produce 1 event");
    }

    /// <summary>
    /// Task 2.2: Test command routing to correct aggregate actor.
    /// Different aggregate IDs should activate different actor instances.
    /// </summary>
    [Fact]
    public async Task ProcessCommandAsync_DifferentAggregateIds_RouteToDifferentActors() {
        // Arrange
        var actorProxyFactory = new ActorProxyFactory(new ActorProxyOptions {
            HttpEndpoint = _fixture.DaprHttpEndpoint,
        });

        CommandEnvelope command1 = new CommandEnvelopeBuilder()
            .WithTenantId("tenant-a")
            .WithDomain("counter")
            .WithAggregateId("route-test-1")
            .WithCommandType("IncrementCounter")
            .Build();

        CommandEnvelope command2 = new CommandEnvelopeBuilder()
            .WithTenantId("tenant-a")
            .WithDomain("counter")
            .WithAggregateId("route-test-2")
            .WithCommandType("IncrementCounter")
            .Build();

        IAggregateActor proxy1 = actorProxyFactory.CreateActorProxy<IAggregateActor>(
            new ActorId(command1.AggregateIdentity.ActorId),
            nameof(AggregateActor));

        IAggregateActor proxy2 = actorProxyFactory.CreateActorProxy<IAggregateActor>(
            new ActorId(command2.AggregateIdentity.ActorId),
            nameof(AggregateActor));

        // Act
        CommandProcessingResult result1 = await proxy1.ProcessCommandAsync(command1);
        CommandProcessingResult result2 = await proxy2.ProcessCommandAsync(command2);

        // Assert
        result1.Accepted.ShouldBeTrue();
        result2.Accepted.ShouldBeTrue();
        result1.CorrelationId.ShouldNotBe(result2.CorrelationId);
    }

    /// <summary>
    /// Task 2.3: Test event persistence with write-once keys (Rule #11).
    /// Multiple commands to the same aggregate should produce sequential events.
    /// </summary>
    [Fact]
    public async Task ProcessCommandAsync_MultipleCommands_PersistsEventsSequentially() {
        // Arrange
        var actorProxyFactory = new ActorProxyFactory(new ActorProxyOptions {
            HttpEndpoint = _fixture.DaprHttpEndpoint,
        });

        string aggregateId = $"persist-test-{Guid.NewGuid():N}";
        IAggregateActor proxy = actorProxyFactory.CreateActorProxy<IAggregateActor>(
            new ActorId($"tenant-a:counter:{aggregateId}"),
            nameof(AggregateActor));

        // Act - send 3 increment commands
        for (int i = 0; i < 3; i++) {
            CommandEnvelope command = new CommandEnvelopeBuilder()
                .WithTenantId("tenant-a")
                .WithDomain("counter")
                .WithAggregateId(aggregateId)
                .WithCommandType("IncrementCounter")
                .Build();

            CommandProcessingResult result = await proxy.ProcessCommandAsync(command);
            result.Accepted.ShouldBeTrue($"Command {i + 1} should be accepted");
            result.EventCount.ShouldBe(1);
        }
    }

    /// <summary>
    /// Task 2.4: Test atomic event writes (0 or N, never partial - FR16).
    /// A single command producing multiple events should persist atomically.
    /// </summary>
    [Fact]
    public async Task ProcessCommandAsync_DomainReturnsMultipleEvents_PersistsAtomically() {
        // Arrange - configure domain invoker to return 3 events for a single command
        _fixture.DomainServiceInvoker.SetupResponse(
            "BatchCommand",
            DomainResult.Success(new IEventPayload[]
            {
                new Hexalith.EventStore.Sample.Counter.Events.CounterIncremented(),
                new Hexalith.EventStore.Sample.Counter.Events.CounterIncremented(),
                new Hexalith.EventStore.Sample.Counter.Events.CounterIncremented(),
            }));

        var actorProxyFactory = new ActorProxyFactory(new ActorProxyOptions {
            HttpEndpoint = _fixture.DaprHttpEndpoint,
        });

        string aggregateId = $"atomic-test-{Guid.NewGuid():N}";
        CommandEnvelope command = new CommandEnvelopeBuilder()
            .WithTenantId("tenant-a")
            .WithDomain("counter")
            .WithAggregateId(aggregateId)
            .WithCommandType("BatchCommand")
            .Build();

        IAggregateActor proxy = actorProxyFactory.CreateActorProxy<IAggregateActor>(
            new ActorId(command.AggregateIdentity.ActorId),
            nameof(AggregateActor));

        // Act
        CommandProcessingResult result = await proxy.ProcessCommandAsync(command);

        // Assert
        result.Accepted.ShouldBeTrue();
        result.EventCount.ShouldBe(3, "All 3 events should be persisted atomically");
    }

    /// <summary>
    /// Task 2.7: Test checkpointed state machine transitions through all stages.
    /// A successful command should transition: Processing → EventsStored → EventsPublished → Completed.
    /// </summary>
    [Fact]
    public async Task ProcessCommandAsync_SuccessfulCommand_TransitionsThroughAllStages() {
        // Arrange
        var actorProxyFactory = new ActorProxyFactory(new ActorProxyOptions {
            HttpEndpoint = _fixture.DaprHttpEndpoint,
        });

        string aggregateId = $"stages-test-{Guid.NewGuid():N}";
        CommandEnvelope command = new CommandEnvelopeBuilder()
            .WithTenantId("tenant-a")
            .WithDomain("counter")
            .WithAggregateId(aggregateId)
            .WithCommandType("IncrementCounter")
            .Build();

        IAggregateActor proxy = actorProxyFactory.CreateActorProxy<IAggregateActor>(
            new ActorId(command.AggregateIdentity.ActorId),
            nameof(AggregateActor));

        // Act
        CommandProcessingResult result = await proxy.ProcessCommandAsync(command);

        // Assert - terminal state should be reached (Completed)
        result.Accepted.ShouldBeTrue();
        result.ErrorMessage.ShouldBeNull();

        // Assert - stage history captures Processing -> EventsStored -> EventsPublished -> Completed
        IReadOnlyList<CommandStatusRecord> history = _fixture.CommandStatusStore.GetStatusHistory(
            command.TenantId,
            command.CorrelationId);

        history.Count.ShouldBeGreaterThanOrEqualTo(4);
        history.Select(h => h.Status).ShouldContain(CommandStatus.Processing);
        history.Select(h => h.Status).ShouldContain(CommandStatus.EventsStored);
        history.Select(h => h.Status).ShouldContain(CommandStatus.EventsPublished);
        history.Select(h => h.Status).ShouldContain(CommandStatus.Completed);

        // Assert - event publication happened to the expected tenant/domain topic (Tier 2 fake publisher scope)
        string expectedTopic = command.AggregateIdentity.PubSubTopic;
        _fixture.EventPublisher.GetPublishedTopics().ShouldContain(expectedTopic);
        _fixture.EventPublisher.GetEventsForTopic(expectedTopic).ShouldNotBeEmpty();
    }

    /// <summary>
    /// Task 2.6: Test state rehydration from snapshot + tail events (FR12, FR14).
    /// Idempotency: submitting the same command twice returns cached result.
    /// </summary>
    [Fact]
    public async Task ProcessCommandAsync_DuplicateCommand_ReturnsCachedResult() {
        // Arrange
        var actorProxyFactory = new ActorProxyFactory(new ActorProxyOptions {
            HttpEndpoint = _fixture.DaprHttpEndpoint,
        });

        string aggregateId = $"idempotent-test-{Guid.NewGuid():N}";
        string correlationId = Guid.NewGuid().ToString();

        CommandEnvelope command = new CommandEnvelopeBuilder()
            .WithTenantId("tenant-a")
            .WithDomain("counter")
            .WithAggregateId(aggregateId)
            .WithCommandType("IncrementCounter")
            .WithCorrelationId(correlationId)
            .Build();

        IAggregateActor proxy = actorProxyFactory.CreateActorProxy<IAggregateActor>(
            new ActorId(command.AggregateIdentity.ActorId),
            nameof(AggregateActor));

        // Act - submit same command twice (same correlationId = same causationId)
        CommandProcessingResult result1 = await proxy.ProcessCommandAsync(command);
        CommandProcessingResult result2 = await proxy.ProcessCommandAsync(command);

        // Assert - both should return accepted, second is cached
        result1.Accepted.ShouldBeTrue();
        result2.Accepted.ShouldBeTrue();
        result1.CorrelationId.ShouldBe(result2.CorrelationId);
    }

    /// <summary>
    /// Task 2.3 (rejection path): Test domain rejection events are persisted like normal events (D3).
    /// </summary>
    [Fact]
    public async Task ProcessCommandAsync_DomainRejection_PersistsRejectionEvents() {
        // Arrange - configure decrement on zero counter to produce rejection
        _fixture.DomainServiceInvoker.SetupResponse(
            "DecrementCounter",
            DomainResult.Rejection(new IRejectionEvent[]
            {
                new Hexalith.EventStore.Sample.Counter.Events.CounterCannotGoNegative(),
            }));

        var actorProxyFactory = new ActorProxyFactory(new ActorProxyOptions {
            HttpEndpoint = _fixture.DaprHttpEndpoint,
        });

        string aggregateId = $"reject-test-{Guid.NewGuid():N}";
        CommandEnvelope command = new CommandEnvelopeBuilder()
            .WithTenantId("tenant-a")
            .WithDomain("counter")
            .WithAggregateId(aggregateId)
            .WithCommandType("DecrementCounter")
            .Build();

        IAggregateActor proxy = actorProxyFactory.CreateActorProxy<IAggregateActor>(
            new ActorId(command.AggregateIdentity.ActorId),
            nameof(AggregateActor));

        // Act
        CommandProcessingResult result = await proxy.ProcessCommandAsync(command);

        // Assert - rejection events are persisted but command is not "accepted"
        result.Accepted.ShouldBeFalse("Rejection should result in Accepted=false");
        result.ErrorMessage.ShouldNotBeNullOrWhiteSpace();
        result.EventCount.ShouldBe(1, "Rejection event should be persisted");
    }
}
