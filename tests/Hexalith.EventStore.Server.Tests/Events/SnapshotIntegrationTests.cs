
using Dapr.Actors;
using Dapr.Actors.Client;
using System.Net;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Tests.Fixtures;
using Hexalith.EventStore.Testing.Builders;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Events;
/// <summary>
/// Story 7.4 / AC #2: Snapshot integration tests.
/// Validates snapshot creation and rehydration with real Redis state store.
/// </summary>
[Collection("DaprTestContainer")]
public class SnapshotIntegrationTests {
    private readonly DaprTestContainerFixture _fixture;

    public SnapshotIntegrationTests(DaprTestContainerFixture fixture) {
        _fixture = fixture;
        _fixture.SetupCounterDomain();
    }

    /// <summary>
    /// Task 2.6: Test state rehydration from events.
    /// After persisting events, the actor should rehydrate state correctly on the next command.
    /// </summary>
    [Fact]
    public async Task ProcessCommandAsync_AfterMultipleEvents_RehydratesStateCorrectly() {
        // Arrange
        var actorProxyFactory = new ActorProxyFactory(new ActorProxyOptions {
            HttpEndpoint = _fixture.DaprHttpEndpoint,
        });

        string aggregateId = $"rehydrate-test-{Guid.NewGuid():N}";
        var identity = new AggregateIdentity("tenant-a", "counter", aggregateId);
        IAggregateActor proxy = actorProxyFactory.CreateActorProxy<IAggregateActor>(
            new ActorId($"tenant-a:counter:{aggregateId}"),
            nameof(AggregateActor));

        // Act - send 5 increments (event replay should work for subsequent commands)
        for (int i = 0; i < 5; i++) {
            CommandEnvelope command = new CommandEnvelopeBuilder()
                .WithTenantId("tenant-a")
                .WithDomain("counter")
                .WithAggregateId(aggregateId)
                .WithCommandType("IncrementCounter")
                .Build();

            CommandProcessingResult result = await proxy.ProcessCommandAsync(command);
            result.Accepted.ShouldBeTrue($"Increment {i + 1} should succeed");
        }

        // Build enough history to cross snapshot interval and force snapshot + tail scenario
        for (int i = 0; i < 15; i++) {
            CommandEnvelope command = new CommandEnvelopeBuilder()
                .WithTenantId("tenant-a")
                .WithDomain("counter")
                .WithAggregateId(aggregateId)
                .WithCommandType("IncrementCounter")
                .Build();

            CommandProcessingResult result = await proxy.ProcessCommandAsync(command);
            result.Accepted.ShouldBeTrue($"Tail increment {i + 1} should succeed");
        }

        string snapshotJson = await GetStateJsonAsync(identity.SnapshotKey);
        snapshotJson.ShouldNotBeNullOrWhiteSpace();
        using JsonDocument snapshotDoc = JsonDocument.Parse(snapshotJson);
        _ = snapshotDoc.RootElement.GetProperty("SequenceNumber").GetInt64();

        long beforeFinalSequence = await GetCurrentSequenceAsync(identity.MetadataKey);

        // Send a final command - the actor should have rehydrated state from previous events
        CommandEnvelope finalCommand = new CommandEnvelopeBuilder()
            .WithTenantId("tenant-a")
            .WithDomain("counter")
            .WithAggregateId(aggregateId)
            .WithCommandType("IncrementCounter")
            .Build();

        CommandProcessingResult finalResult = await proxy.ProcessCommandAsync(finalCommand);

        // Assert
        finalResult.Accepted.ShouldBeTrue("Post-rehydration command should succeed");
        finalResult.EventCount.ShouldBe(1);

        long afterFinalSequence = await GetCurrentSequenceAsync(identity.MetadataKey);
        afterFinalSequence.ShouldBe(beforeFinalSequence + 1);
    }

    private async Task<string> GetStateJsonAsync(string key) {
        using var http = new HttpClient();
        string url = $"{_fixture.DaprHttpEndpoint}/v1.0/state/statestore/{Uri.EscapeDataString(key)}";

        using HttpResponseMessage response = await http.GetAsync(url);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        return await response.Content.ReadAsStringAsync();
    }

    private async Task<long> GetCurrentSequenceAsync(string metadataKey) {
        string metadataJson = await GetStateJsonAsync(metadataKey);
        using JsonDocument doc = JsonDocument.Parse(metadataJson);
        return doc.RootElement.GetProperty("CurrentSequence").GetInt64();
    }
}
