using System.Text.Json;

using Shouldly;

using ServerEventEnvelope = Hexalith.EventStore.Server.Events.EventEnvelope;

namespace Hexalith.EventStore.Server.Tests.DomainServices;

public class DaprSerializationRoundTripTests
{
    /// <summary>
    /// Verifies that the EventEnvelope Payload field round-trips correctly through
    /// the Web JSON serialization that Dapr uses for service invocation.
    /// </summary>
    [Fact]
    public void EventEnvelope_Payload_RoundTrips_AsBase64_WithWebDefaults()
    {
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(
            new Hexalith.EventStore.Sample.Counter.Events.CounterIncremented());
        var envelope = new ServerEventEnvelope(
            MessageId: "msg-1",
            AggregateId: "counter-1",
            AggregateType: "test-aggregate",
            TenantId: "tenant-a",
            Domain: "counter",
            SequenceNumber: 1,
            GlobalPosition: 0,
            Timestamp: DateTimeOffset.UtcNow,
            CorrelationId: "corr-1",
            CausationId: "corr-1",
            UserId: "user-1",
            DomainServiceVersion: "v1",
            EventTypeName: typeof(Hexalith.EventStore.Sample.Counter.Events.CounterIncremented).FullName!,
            MetadataVersion: 1,
            SerializationFormat: "json",
            Payload: payload,
            Extensions: null);

        var webOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        string json = JsonSerializer.Serialize(envelope, webOptions);

        json.ShouldContain("\"payload\":");
        json.ShouldContain("\"eventTypeName\":");

        JsonElement parsed = JsonSerializer.Deserialize<JsonElement>(json, webOptions);
        parsed.TryGetProperty("payload", out JsonElement payloadElement).ShouldBeTrue();
        payloadElement.ValueKind.ShouldBe(
            JsonValueKind.String,
            "byte[] Payload should serialize as a Base64 string with Web defaults");

        byte[] decoded = payloadElement.GetBytesFromBase64();
        decoded.ShouldBe(payload);
    }
}
