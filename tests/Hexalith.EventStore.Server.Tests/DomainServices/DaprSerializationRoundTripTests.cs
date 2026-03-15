
using System.Text.Json;

using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Client.Aggregates;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Server.Actors;
using ServerEventEnvelope = Hexalith.EventStore.Server.Events.EventEnvelope;
using Hexalith.EventStore.Server.Tests.Fixtures;
using Hexalith.EventStore.Testing.Builders;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.DomainServices;

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
public class DaprSerializationRoundTripTests {
    private readonly DaprTestContainerFixture _fixture;

    public DaprSerializationRoundTripTests(DaprTestContainerFixture fixture) {
        _fixture = fixture;
        _fixture.SetupCounterDomain();
    }

    // --- Test aggregate using actual sample event types to match stored eventTypeName ---
    private sealed record TestDecrementCounter;

    private sealed class RoundTripCounterState {
        public int Count { get; private set; }

        public void Apply(Hexalith.EventStore.Sample.Counter.Events.CounterIncremented e) => Count++;

        public void Apply(Hexalith.EventStore.Sample.Counter.Events.CounterDecremented e) => Count--;

        public void Apply(Hexalith.EventStore.Sample.Counter.Events.CounterReset e) => Count = 0;
    }

    private sealed class RoundTripCounterAggregate : EventStoreAggregate<RoundTripCounterState> {
        public static DomainResult Handle(TestDecrementCounter command, RoundTripCounterState? state) {
            if ((state?.Count ?? 0) == 0) {
                return DomainResult.Rejection(System.Array.Empty<IRejectionEvent>());
            }

            return DomainResult.Success(new IEventPayload[] { new Hexalith.EventStore.Sample.Counter.Events.CounterDecremented() });
        }
    }

    /// <summary>
    /// Verifies that events stored in Dapr Redis by the AggregateActor can survive
    /// the JSON serialization round-trip that occurs during domain service invocation.
    /// This is the exact scenario that caused the production bug: byte[] Payload is
    /// serialized as Base64, and the receiving EventStoreAggregate must decode it.
    /// </summary>
    [Fact]
    public async Task StoredEvents_SurviveDaprSerializationRoundTrip_WhenPassedToDomainService() {
        // Arrange: Send two commands to store events in Redis via the real Dapr actor
        _fixture.ThrowIfHostStopped();

        var actorProxyFactory = new ActorProxyFactory(new ActorProxyOptions {
            HttpEndpoint = _fixture.DaprHttpEndpoint,
        });

        string aggregateId = $"roundtrip-{Guid.NewGuid():N}";
        IAggregateActor proxy = actorProxyFactory.CreateActorProxy<IAggregateActor>(
            new ActorId($"tenant-a:counter:{aggregateId}"),
            nameof(AggregateActor));

        // Send 2 increment commands — this stores 2 EventEnvelope records in Redis
        for (int i = 0; i < 2; i++) {
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
        (CommandEnvelope _, object? capturedState) = _fixture.DomainServiceInvoker.InvocationsWithState
            .Last(i => i.Command.AggregateId == aggregateId);

        // The captured state should be a List<EventEnvelope> with at least 1 event
        capturedState.ShouldNotBeNull("AggregateActor should pass non-null state after first event");
        capturedState.ShouldBeAssignableTo<IEnumerable<ServerEventEnvelope>>();
        var events = ((IEnumerable<ServerEventEnvelope>)capturedState).ToList();
        events.Count.ShouldBeGreaterThan(0, "Should have stored events from previous commands");

        // Simulate the Dapr DomainServiceRequest serialization round-trip:
        // DaprClient serializes with JsonSerializerDefaults.Web (byte[] → Base64 string)
        // ASP.NET Core deserializes to DomainServiceRequest where CurrentState becomes JsonElement
        var webOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        byte[] serialized = JsonSerializer.SerializeToUtf8Bytes(events, webOptions);
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

    /// <summary>
    /// Verifies that the EventEnvelope Payload field round-trips correctly through
    /// the Web JSON serialization that Dapr uses for service invocation.
    /// </summary>
    [Fact]
    public void EventEnvelope_Payload_RoundTrips_AsBase64_WithWebDefaults() {
        // Arrange: Create an EventEnvelope with a parameterless record payload
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(
            new Hexalith.EventStore.Sample.Counter.Events.CounterIncremented());
        var envelope = new ServerEventEnvelope(
            AggregateId: "counter-1",
            TenantId: "tenant-a",
            Domain: "counter",
            SequenceNumber: 1,
            Timestamp: DateTimeOffset.UtcNow,
            CorrelationId: "corr-1",
            CausationId: "corr-1",
            UserId: "user-1",
            DomainServiceVersion: "v1",
            EventTypeName: typeof(Hexalith.EventStore.Sample.Counter.Events.CounterIncremented).FullName!,
            SerializationFormat: "json",
            Payload: payload,
            Extensions: null);

        // Act: Serialize with Web defaults (as DaprClient does)
        var webOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        string json = JsonSerializer.Serialize(envelope, webOptions);

        // Assert: Payload should be Base64-encoded, and the JSON should contain both
        // the "payload" key and the "eventTypeName" key in camelCase
        json.ShouldContain("\"payload\":");
        json.ShouldContain("\"eventTypeName\":");

        // Verify the payload is a Base64 string (not a JSON object)
        JsonElement parsed = JsonSerializer.Deserialize<JsonElement>(json, webOptions);
        parsed.TryGetProperty("payload", out JsonElement payloadElement).ShouldBeTrue();
        payloadElement.ValueKind.ShouldBe(JsonValueKind.String,
            "byte[] Payload should serialize as a Base64 string with Web defaults");

        // Verify the Base64 can be decoded back to the original payload
        byte[] decoded = payloadElement.GetBytesFromBase64();
        decoded.ShouldBe(payload);
    }
}
