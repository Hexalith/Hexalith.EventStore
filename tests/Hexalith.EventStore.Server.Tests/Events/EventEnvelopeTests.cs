namespace Hexalith.EventStore.Server.Tests.Events;

using System.Text.Json;

using Hexalith.EventStore.Server.Events;

using Shouldly;

public class EventEnvelopeTests
{
    private static EventEnvelope CreateTestEnvelope(
        IDictionary<string, string>? extensions = null) => new(
        AggregateId: "agg-001",
        TenantId: "test-tenant",
        Domain: "test-domain",
        SequenceNumber: 1,
        Timestamp: new DateTimeOffset(2026, 1, 15, 10, 30, 0, TimeSpan.Zero),
        CorrelationId: "corr-1",
        CausationId: "cause-1",
        UserId: "user-1",
        DomainServiceVersion: "1.0.0",
        EventTypeName: "OrderCreated",
        SerializationFormat: "json",
        Payload: [1, 2, 3, 4, 5],
        Extensions: extensions);

    [Fact]
    public void EventEnvelope_JsonRoundtrip_PreservesAllFields()
    {
        // Arrange
        var extensions = new Dictionary<string, string> { ["key1"] = "val1", ["key2"] = "val2" };
        EventEnvelope original = CreateTestEnvelope(extensions);

        // Act
        string json = JsonSerializer.Serialize(original);
        EventEnvelope? deserialized = JsonSerializer.Deserialize<EventEnvelope>(json);

        // Assert
        deserialized.ShouldNotBeNull();
        deserialized.AggregateId.ShouldBe(original.AggregateId);
        deserialized.TenantId.ShouldBe(original.TenantId);
        deserialized.Domain.ShouldBe(original.Domain);
        deserialized.SequenceNumber.ShouldBe(original.SequenceNumber);
        deserialized.Timestamp.ShouldBe(original.Timestamp);
        deserialized.CorrelationId.ShouldBe(original.CorrelationId);
        deserialized.CausationId.ShouldBe(original.CausationId);
        deserialized.UserId.ShouldBe(original.UserId);
        deserialized.DomainServiceVersion.ShouldBe(original.DomainServiceVersion);
        deserialized.EventTypeName.ShouldBe(original.EventTypeName);
        deserialized.SerializationFormat.ShouldBe(original.SerializationFormat);
        deserialized.Payload.ShouldBe(original.Payload);
        deserialized.Extensions.ShouldNotBeNull();
        deserialized.Extensions!["key1"].ShouldBe("val1");
        deserialized.Extensions["key2"].ShouldBe("val2");
    }

    [Fact]
    public void EventEnvelope_ByteArrayPayload_SerializesAsBase64()
    {
        // Arrange
        EventEnvelope envelope = CreateTestEnvelope();

        // Act
        string json = JsonSerializer.Serialize(envelope);

        // Assert -- byte[] serializes as base64 in System.Text.Json
        json.ShouldContain(Convert.ToBase64String(envelope.Payload));
    }

    [Fact]
    public void EventEnvelope_NullExtensions_RoundtripsCorrectly()
    {
        // Arrange
        EventEnvelope original = CreateTestEnvelope(extensions: null);

        // Act
        string json = JsonSerializer.Serialize(original);
        EventEnvelope? deserialized = JsonSerializer.Deserialize<EventEnvelope>(json);

        // Assert
        deserialized.ShouldNotBeNull();
        deserialized.Extensions.ShouldBeNull();
    }

    [Fact]
    public void EventEnvelope_Identity_ReturnsDerivedAggregateIdentity()
    {
        // Arrange
        EventEnvelope envelope = CreateTestEnvelope();

        // Act
        var identity = envelope.Identity;

        // Assert
        identity.TenantId.ShouldBe("test-tenant");
        identity.Domain.ShouldBe("test-domain");
        identity.AggregateId.ShouldBe("agg-001");
    }
}
