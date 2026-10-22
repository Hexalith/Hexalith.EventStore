
using System.Text.Json;

using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Client.Aggregates;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;
using Hexalith.EventStore.Testing.Builders;

using Shouldly;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.DomainServices;

/// <summary>
/// Tier 2 integration tests that verify the Dapr serialization round-trip for event payloads.
/// The bug: EventEnvelope.Payload (byte[]) is serialized as Base64 by System.Text.Json when
/// passing through DaprClient.InvokeMethodAsync. On the receiving side, EventStoreAggregate
/// must correctly decode the Base64 string and deserialize the event payload.
/// These tests store events via the real Dapr actor state store (Redis), capture the rehydrated
/// state, and verify it can survive the serialization round-trip that occurs during domain
/// service invocation.
/// </summary>
[Collection("DaprTestContainer")]
[Trait("Category", "LiveSidecar")]
public class DaprSerializationRoundTripTests
{
    private readonly DaprTestContainerFixture _fixture;

    public DaprSerializationRoundTripTests(DaprTestContainerFixture fixture)
    {
        _fixture = fixture;
        _fixture.SetupCounterDomain();
    }

    /// <summary>
    /// Verifies that events stored in Dapr Redis by the AggregateActor can survive
    /// the JSON serialization round-trip that occurs during domain service invocation.
    /// This is the exact scenario that caused the production bug: byte[] Payload is
    /// serialized as Base64, and the receiving EventStoreAggregate must decode it.
    /// </summary>
    [Fact]
    public async Task StoredEvents_SurviveDaprSerializationRoundTrip_WhenPassedToDomainService()
    {
        // Arrange: Send two commands to store events in Redis via the real Dapr actor
        _fixture.ThrowIfHostStopped();

        var actorProxyFactory = new ActorProxyFactory(new ActorProxyOptions
        {
            HttpEndpoint = _fixture.DaprHttpEndpoint,
        });

        string aggregateId = $"roundtrip-{Guid.NewGuid():N}";
        IAggregateActor proxy = actorProxyFactory.CreateActorProxy<IAggregateActor>(
            new ActorId($"tenant-a:counter:{aggregateId}"),
            _fixture.AggregateActorTypeName);

        // Send 2 increment commands — this stores 2 EventEnvelope records in Redis
        for (int i = 0; i < 2; i++)
        {
            CommandEnvelope command = new CommandEnvelopeBuilder()
                .WithTenantId("tenant-a")
                .WithDomain("counter")
                .WithAggregateId(aggregateId)
                .WithCommandType("IncrementCounter")
                .Build();

            CommandProcessingResult result = await proxy.ProcessCommandAsync(command);
            result.Accepted.ShouldBeTrue($"Command {i + 1} should be accepted");
        }

        // Act: Capture the currentState that the AggregateActor passed to the domain service
        // on the 2nd invocation (which includes the event from the 1st command)
        _fixture.DomainServiceInvoker.InvocationsWithState.Count.ShouldBeGreaterThanOrEqualTo(2);
        (_, object? capturedState) = _fixture.DomainServiceInvoker.InvocationsWithState
            .Last(i => i.Command.AggregateId == aggregateId);

        // The captured state should be a DomainServiceCurrentState with at least 1 event
        _ = capturedState.ShouldNotBeNull("AggregateActor should pass non-null state after first event");
        _ = capturedState.ShouldBeAssignableTo<DomainServiceCurrentState>();
        var state = (DomainServiceCurrentState)capturedState;
        state.Events.Count.ShouldBeGreaterThan(0, "Should have stored events from previous commands");

        // Simulate the Dapr DomainServiceRequest serialization round-trip:
        // DaprClient serializes with JsonSerializerDefaults.Web (byte[] → Base64 string)
        // ASP.NET Core deserializes to DomainServiceRequest where CurrentState becomes JsonElement.
        // CurrentState now carries DomainServiceCurrentState (snapshot + events tail), not a raw event list.
        var webOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        byte[] serialized = JsonSerializer.SerializeToUtf8Bytes(state, webOptions);
        JsonElement deserializedState = JsonSerializer.Deserialize<JsonElement>(serialized, webOptions);

        // Assert: EventStoreAggregate must handle the Base64-encoded payloads
        var aggregate = new RoundTripCounterAggregate();
        CommandEnvelope testCommand = new CommandEnvelopeBuilder()
            .WithTenantId("tenant-a")
            .WithDomain("counter")
            .WithAggregateId(aggregateId)
            .WithCommandType(nameof(TestDecrementCounter))
            .Build();

        // This is the call that fails without the fix — the Base64 payload deserialization
        // throws JsonException in ApplyEventByName
        DomainResult domainResult = await aggregate.ProcessAsync(testCommand, deserializedState);

        // With 1+ increments rehydrated, decrement should succeed
        domainResult.IsSuccess.ShouldBeTrue(
            "Domain service should process command after rehydrating state from Dapr-serialized events");
    }

}
