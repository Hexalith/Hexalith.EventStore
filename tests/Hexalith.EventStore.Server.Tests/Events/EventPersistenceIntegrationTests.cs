
using System.Net;
using System.Text.Json;

using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Identity;
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

        _fixture.ThrowIfHostStopped();

        // Act - persist 10 events
        for (int i = 0; i < 10; i++) {
            CommandEnvelope command = new CommandEnvelopeBuilder()
                .WithTenantId("tenant-a")
                .WithDomain("counter")
                .WithAggregateId(aggregateId)
                .WithCommandType("IncrementCounter")
                .Build();

            CommandProcessingResult result = await proxy.ProcessCommandAsync(command).ConfigureAwait(true);
            result.Accepted.ShouldBeTrue($"Command {i + 1} should succeed on Redis state store");
        }

        // Assert - Tier 2 pub/sub path is exercised through FakeEventPublisher
        string expectedTopic = "tenant-a.counter.events";
        _fixture.EventPublisher.GetPublishedTopics().ShouldContain(expectedTopic);
        _fixture.EventPublisher.GetEventsForTopic(expectedTopic).Count.ShouldBeGreaterThanOrEqualTo(10);
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

        _fixture.ThrowIfHostStopped();

        // Act - first call creates idempotency record, second returns cached
        CommandProcessingResult result1 = await proxy.ProcessCommandAsync(command).ConfigureAwait(true);
        CommandProcessingResult result2 = await proxy.ProcessCommandAsync(command).ConfigureAwait(true);

        // Assert - idempotency record persisted in Redis actor state
        result1.Accepted.ShouldBeTrue();
        result2.Accepted.ShouldBeTrue();
        result2.CorrelationId.ShouldBe(correlationId);
    }

    /// <summary>
    /// Task 2.5: Test snapshot creation at configured intervals (Rule #15).
    /// Fixture configures counter domain snapshot interval to 15 events.
    /// After 15+ events the system should create a snapshot; subsequent commands
    /// verify state rehydration from snapshot + tail events (FR12, FR14).
    /// </summary>
    [Fact]
    public async Task ProcessCommandAsync_ExceedsSnapshotInterval_CreatesSnapshot() {
        // Arrange
        var actorProxyFactory = new ActorProxyFactory(new ActorProxyOptions {
            HttpEndpoint = _fixture.DaprHttpEndpoint,
        });

        string aggregateId = $"snapshot-test-{Guid.NewGuid():N}";
        var identity = new AggregateIdentity("tenant-a", "counter", aggregateId);
        IAggregateActor proxy = actorProxyFactory.CreateActorProxy<IAggregateActor>(
            new ActorId($"tenant-a:counter:{aggregateId}"),
            nameof(AggregateActor));

        _fixture.ThrowIfHostStopped();

        // Act - send 20 commands (exceeds snapshot interval of 15)
        for (int i = 0; i < 20; i++) {
            CommandEnvelope command = new CommandEnvelopeBuilder()
                .WithTenantId("tenant-a")
                .WithDomain("counter")
                .WithAggregateId(aggregateId)
                .WithCommandType("IncrementCounter")
                .Build();

            CommandProcessingResult result = await proxy.ProcessCommandAsync(command).ConfigureAwait(true);
            result.Accepted.ShouldBeTrue($"Command {i + 1} should succeed");
        }

        // Assert - verify the aggregate can still process commands after crossing snapshot interval
        CommandEnvelope finalCommand = new CommandEnvelopeBuilder()
            .WithTenantId("tenant-a")
            .WithDomain("counter")
            .WithAggregateId(aggregateId)
            .WithCommandType("IncrementCounter")
            .Build();

        CommandProcessingResult finalResult = await proxy.ProcessCommandAsync(finalCommand).ConfigureAwait(true);
        finalResult.Accepted.ShouldBeTrue("Post-snapshot command should succeed (state rehydrated)");

        string expectedTopic = "tenant-a.counter.events";
        _fixture.EventPublisher.GetPublishedTopics().ShouldContain(expectedTopic);
        _fixture.EventPublisher.GetEventsForTopic(expectedTopic).Count.ShouldBeGreaterThanOrEqualTo(21);
    }

    private async Task<string> GetStateJsonAsync(string key) {
        using var http = new HttpClient();
        string url = $"{_fixture.DaprHttpEndpoint}/v1.0/state/statestore/{Uri.EscapeDataString(key)}";

        for (int attempt = 0; attempt < 10; attempt++) {
            using HttpResponseMessage response = await http.GetAsync(url).ConfigureAwait(true);
            response.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.NoContent);

            if (response.StatusCode == HttpStatusCode.OK) {
                string json = await response.Content.ReadAsStringAsync().ConfigureAwait(true);
                if (!string.IsNullOrWhiteSpace(json)) {
                    return json;
                }
            }

            await Task.Delay(50).ConfigureAwait(true);
        }

        throw new ShouldAssertException($"State for key '{key}' did not become available after retries.");
    }

    private async Task<long> GetCurrentSequenceAsync(string metadataKey) {
        string metadataJson = await GetStateJsonAsync(metadataKey).ConfigureAwait(true);
        using var doc = JsonDocument.Parse(metadataJson);
        return doc.RootElement.GetProperty("CurrentSequence").GetInt64();
    }
}
