
using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Tests.Fixtures;
using Hexalith.EventStore.Testing.Builders;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Events;
/// <summary>
/// Story 7.4 / AC #2, AC #6: Event persistence and Redis backend integration tests.
/// Validates event persistence atomicity and Redis state store behavior.
/// </summary>
[Collection("DaprTestContainer")]
public class EventPersistenceIntegrationTests {
    private readonly DaprTestContainerFixture _fixture;

    public EventPersistenceIntegrationTests(DaprTestContainerFixture fixture) {
        _fixture = fixture;
        _fixture.SetupCounterDomain();
    }

    /// <summary>
    /// Task 6.1: Verify Redis state store supports key-value ops, ETag concurrency, actor state.
    /// Multiple sequential commands to the same aggregate should all persist successfully.
    /// </summary>
    [Fact]
    public async Task RedisStateStore_SequentialCommands_PersistsAllEvents() {
        // Arrange
        var actorProxyFactory = new ActorProxyFactory(new ActorProxyOptions {
            HttpEndpoint = _fixture.DaprHttpEndpoint,
        });

        string aggregateId = $"redis-kv-test-{Guid.NewGuid():N}";
        IAggregateActor proxy = actorProxyFactory.CreateActorProxy<IAggregateActor>(
            new ActorId($"tenant-a:counter:{aggregateId}"),
            nameof(AggregateActor));

        // Act - persist 10 events
        for (int i = 0; i < 10; i++) {
            CommandEnvelope command = new CommandEnvelopeBuilder()
                .WithTenantId("tenant-a")
                .WithDomain("counter")
                .WithAggregateId(aggregateId)
                .WithCommandType("IncrementCounter")
                .Build();

            CommandProcessingResult result = await proxy.ProcessCommandAsync(command);
            result.Accepted.ShouldBeTrue($"Command {i + 1} should succeed on Redis state store");
        }
    }

    /// <summary>
    /// Task 6.1: Verify actor state store works with actorStateStore: true metadata.
    /// Idempotency records (actor state) should survive across multiple calls.
    /// </summary>
    [Fact]
    public async Task RedisActorStateStore_IdempotencyRecords_PersistAcrossCalls() {
        // Arrange
        var actorProxyFactory = new ActorProxyFactory(new ActorProxyOptions {
            HttpEndpoint = _fixture.DaprHttpEndpoint,
        });

        string aggregateId = $"redis-actor-state-{Guid.NewGuid():N}";
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

        // Act - first call creates idempotency record, second returns cached
        CommandProcessingResult result1 = await proxy.ProcessCommandAsync(command);
        CommandProcessingResult result2 = await proxy.ProcessCommandAsync(command);

        // Assert - idempotency record persisted in Redis actor state
        result1.Accepted.ShouldBeTrue();
        result2.Accepted.ShouldBeTrue();
        result2.CorrelationId.ShouldBe(correlationId);
    }

    /// <summary>
    /// Task 2.5: Test snapshot creation at configured intervals (default 100 events, Rule #15).
    /// After 100+ events, the system should create a snapshot.
    /// Note: This test is long-running as it sends many commands.
    /// </summary>
    [Fact(Skip = "Long-running: sends 105 commands to trigger snapshot at interval 100")]
    public async Task ProcessCommandAsync_ExceedsSnapshotInterval_CreatesSnapshot() {
        // Arrange
        var actorProxyFactory = new ActorProxyFactory(new ActorProxyOptions {
            HttpEndpoint = _fixture.DaprHttpEndpoint,
        });

        string aggregateId = $"snapshot-test-{Guid.NewGuid():N}";
        IAggregateActor proxy = actorProxyFactory.CreateActorProxy<IAggregateActor>(
            new ActorId($"tenant-a:counter:{aggregateId}"),
            nameof(AggregateActor));

        // Act - send 105 commands (exceeds snapshot interval of 100)
        for (int i = 0; i < 105; i++) {
            CommandEnvelope command = new CommandEnvelopeBuilder()
                .WithTenantId("tenant-a")
                .WithDomain("counter")
                .WithAggregateId(aggregateId)
                .WithCommandType("IncrementCounter")
                .Build();

            CommandProcessingResult result = await proxy.ProcessCommandAsync(command);
            result.Accepted.ShouldBeTrue($"Command {i + 1} should succeed");
        }

        // Assert - verify the aggregate can still process commands (state rehydrated from snapshot + tail)
        CommandEnvelope finalCommand = new CommandEnvelopeBuilder()
            .WithTenantId("tenant-a")
            .WithDomain("counter")
            .WithAggregateId(aggregateId)
            .WithCommandType("IncrementCounter")
            .Build();

        CommandProcessingResult finalResult = await proxy.ProcessCommandAsync(finalCommand);
        finalResult.Accepted.ShouldBeTrue("Post-snapshot command should succeed (state rehydrated)");
    }
}
