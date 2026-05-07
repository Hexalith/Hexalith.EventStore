
using System.Text.Json;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Events;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Events;

public class EventEnvelopeTests {
    private static EventEnvelope CreateTestEnvelope(
        IDictionary<string, string>? extensions = null) => new(
        MessageId: "msg-1",
        AggregateId: "agg-001",
        AggregateType: "test-aggregate",
        TenantId: "test-tenant",
        Domain: "test-domain",
        SequenceNumber: 1,
        GlobalPosition: 0,
        Timestamp: new DateTimeOffset(2026, 1, 15, 10, 30, 0, TimeSpan.Zero),
        CorrelationId: "corr-1",
        CausationId: "cause-1",
        UserId: "user-1",
        DomainServiceVersion: "1.0.0",
        EventTypeName: "OrderCreated",
        MetadataVersion: 1,
        SerializationFormat: "json",
        Payload: [1, 2, 3, 4, 5],
        Extensions: extensions);

    [Fact]
    public void EventEnvelope_JsonRoundtrip_PreservesAllFields() {
        // Arrange
        var extensions = new Dictionary<string, string> { ["key1"] = "val1", ["key2"] = "val2" };
        EventEnvelope original = CreateTestEnvelope(extensions);

        // Act
        string json = JsonSerializer.Serialize(original);
        EventEnvelope? deserialized = JsonSerializer.Deserialize<EventEnvelope>(json);

        // Assert
        _ = deserialized.ShouldNotBeNull();
        deserialized.MessageId.ShouldBe(original.MessageId);
        deserialized.AggregateId.ShouldBe(original.AggregateId);
        deserialized.AggregateType.ShouldBe(original.AggregateType);
        deserialized.TenantId.ShouldBe(original.TenantId);
        deserialized.Domain.ShouldBe(original.Domain);
        deserialized.SequenceNumber.ShouldBe(original.SequenceNumber);
        deserialized.GlobalPosition.ShouldBe(original.GlobalPosition);
        deserialized.Timestamp.ShouldBe(original.Timestamp);
        deserialized.CorrelationId.ShouldBe(original.CorrelationId);
        deserialized.CausationId.ShouldBe(original.CausationId);
        deserialized.UserId.ShouldBe(original.UserId);
        deserialized.DomainServiceVersion.ShouldBe(original.DomainServiceVersion);
        deserialized.EventTypeName.ShouldBe(original.EventTypeName);
        deserialized.MetadataVersion.ShouldBe(original.MetadataVersion);
        deserialized.SerializationFormat.ShouldBe(original.SerializationFormat);
        deserialized.Payload.ShouldBe(original.Payload);
        _ = deserialized.Extensions.ShouldNotBeNull();
        deserialized.Extensions!["key1"].ShouldBe("val1");
        deserialized.Extensions["key2"].ShouldBe("val2");
    }

    [Fact]
    public void EventEnvelope_ByteArrayPayload_SerializesAsBase64() {
        // Arrange
        EventEnvelope envelope = CreateTestEnvelope();

        // Act
        string json = JsonSerializer.Serialize(envelope);

        // Assert -- byte[] serializes as base64 in System.Text.Json
        json.ShouldContain(Convert.ToBase64String(envelope.Payload));
    }

    [Fact]
    public void EventEnvelope_NullExtensions_RoundtripsCorrectly() {
        // Arrange
        EventEnvelope original = CreateTestEnvelope(extensions: null);

        // Act
        string json = JsonSerializer.Serialize(original);
        EventEnvelope? deserialized = JsonSerializer.Deserialize<EventEnvelope>(json);

        // Assert
        _ = deserialized.ShouldNotBeNull();
        deserialized.Extensions.ShouldBeNull();
    }

    [Fact]
    public void EventEnvelope_Identity_ReturnsDerivedAggregateIdentity() {
        // Arrange
        EventEnvelope envelope = CreateTestEnvelope();

        // Act
        AggregateIdentity identity = envelope.Identity;

        // Assert
        identity.TenantId.ShouldBe("test-tenant");
        identity.Domain.ShouldBe("test-domain");
        identity.AggregateId.ShouldBe("agg-001");
    }

    /// <summary>
    /// Test ID: 1.1-UNIT-013. Closes Epic-1 R-T2 / TG-1 along the actor-remoting serialization path.
    /// <para>
    /// <see cref="EventEnvelope"/> here is the flat-record DAPR actor-remoting type (17
    /// <c>[property: DataMember]</c> positional parameters). DAPR actor proxy serializes method
    /// parameters via <see cref="System.Runtime.Serialization.DataContractSerializer"/>, and
    /// <c>[property: DataMember]</c> attributes on positional parameters are the exact silent-trap
    /// pattern R-T2 warns about: a typo, a missing attribute on a new field, or an attribute that
    /// vanishes during a refactor produces an envelope where the offending field round-trips as
    /// default — green compile, broken at first actor reactivation. This test pins that every
    /// declared <c>[DataMember]</c> survives a DCS round-trip with the same value, so any future
    /// regression is caught at Tier 1 instead of Tier 2 actor lifecycle replay.
    /// </para>
    /// </summary>
    [Fact]
    public void DataContract_SerializationRoundTrip_PreservesAll17DataMembers() {
        var extensions = new Dictionary<string, string> {
            ["traceparent"] = "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01",
            ["custom"] = "value",
        };
        EventEnvelope original = CreateTestEnvelope(extensions);

        var serializer = new System.Runtime.Serialization.DataContractSerializer(typeof(EventEnvelope));
        using var ms = new System.IO.MemoryStream();
        serializer.WriteObject(ms, original);
        ms.Position = 0;
        EventEnvelope deserialized = (EventEnvelope)serializer.ReadObject(ms)!;

        deserialized.MessageId.ShouldBe(original.MessageId);
        deserialized.AggregateId.ShouldBe(original.AggregateId);
        deserialized.AggregateType.ShouldBe(original.AggregateType);
        deserialized.TenantId.ShouldBe(original.TenantId);
        deserialized.Domain.ShouldBe(original.Domain);
        deserialized.SequenceNumber.ShouldBe(original.SequenceNumber);
        deserialized.GlobalPosition.ShouldBe(original.GlobalPosition);
        deserialized.Timestamp.ShouldBe(original.Timestamp);
        deserialized.CorrelationId.ShouldBe(original.CorrelationId);
        deserialized.CausationId.ShouldBe(original.CausationId);
        deserialized.UserId.ShouldBe(original.UserId);
        deserialized.DomainServiceVersion.ShouldBe(original.DomainServiceVersion);
        deserialized.EventTypeName.ShouldBe(original.EventTypeName);
        deserialized.MetadataVersion.ShouldBe(original.MetadataVersion);
        deserialized.SerializationFormat.ShouldBe(original.SerializationFormat);
        deserialized.Payload.SequenceEqual(original.Payload).ShouldBeTrue();
        _ = deserialized.Extensions.ShouldNotBeNull();
        deserialized.Extensions!["traceparent"].ShouldBe(extensions["traceparent"]);
        deserialized.Extensions["custom"].ShouldBe(extensions["custom"]);
    }
}
