
using System.Collections.ObjectModel;

using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Contracts.Tests.Events;

public class EventEnvelopeTests {
    private static EventMetadata CreateMetadata(long sequenceNumber = 1) =>
        new(
            MessageId: "msg-001",
            AggregateId: "order-123",
            AggregateType: "order",
            TenantId: "acme",
            Domain: "payments",
            SequenceNumber: sequenceNumber,
            GlobalPosition: 0,
            Timestamp: DateTimeOffset.UtcNow,
            CorrelationId: "corr-1",
            CausationId: "cause-1",
            UserId: "user-1",
            DomainServiceVersion: "1.0.0",
            EventTypeName: "OrderCreated",
            MetadataVersion: 1,
            SerializationFormat: "json");

    [Fact]
    public void Constructor_WithValidInputs_CreatesInstance() {
        EventMetadata metadata = CreateMetadata();
        byte[] payload = [1, 2, 3];
        var extensions = new ReadOnlyDictionary<string, string>(new Dictionary<string, string> { ["key"] = "value" });

        var envelope = new EventEnvelope(metadata, payload, extensions);

        envelope.Metadata.ShouldBe(metadata);
        envelope.Payload.ShouldBeSameAs(payload);
        envelope.Extensions.ShouldBe(extensions);
        envelope.Extensions.ShouldNotBeSameAs(extensions); // Defensive copy
    }

    [Fact]
    public void Constructor_WithNullExtensions_StoresEmptyReadOnlyDictionary() {
        EventMetadata metadata = CreateMetadata();
        byte[] payload = [1, 2, 3];

        var envelope = new EventEnvelope(metadata, payload, null);

        _ = envelope.Extensions.ShouldNotBeNull();
        envelope.Extensions.ShouldBeEmpty();
    }

    [Fact]
    public void Extensions_IsIReadOnlyDictionary() {
        EventMetadata metadata = CreateMetadata();
        byte[] payload = [1, 2, 3];
        var dict = new Dictionary<string, string> { ["k"] = "v" };

        var envelope = new EventEnvelope(metadata, payload, dict);

        _ = envelope.Extensions.ShouldBeAssignableTo<IReadOnlyDictionary<string, string>>();
    }

    [Fact]
    public void RecordEquality_SameMetadataDifferentPayloadArrays_AreNotEqual() {
        EventMetadata metadata = CreateMetadata();
        byte[] payload1 = [1, 2, 3];
        byte[] payload2 = [1, 2, 3];

        var envelope1 = new EventEnvelope(metadata, payload1, null);
        var envelope2 = new EventEnvelope(metadata, payload2, null);

        // Record equality uses reference equality for byte[] — documented limitation
        envelope2.ShouldNotBe(envelope1);
    }

    [Fact]
    public void RecordEquality_SamePayloadReference_AreEqual() {
        EventMetadata metadata = CreateMetadata();
        byte[] payload = [1, 2, 3];

        var envelope1 = new EventEnvelope(metadata, payload, null);
        var envelope2 = new EventEnvelope(metadata, payload, null);

        envelope2.ShouldBe(envelope1);
    }

    [Fact]
    public void PayloadComparison_UseSequenceEqual_ForByteComparison() {
        EventMetadata metadata = CreateMetadata();
        byte[] payload1 = [1, 2, 3];
        byte[] payload2 = [1, 2, 3];

        var envelope1 = new EventEnvelope(metadata, payload1, null);
        var envelope2 = new EventEnvelope(metadata, payload2, null);

        envelope1.Payload.SequenceEqual(envelope2.Payload).ShouldBeTrue();
    }

    [Fact]
    public void Constructor_WithNullMetadata_ThrowsArgumentNullException() => Should.Throw<ArgumentNullException>(() => new EventEnvelope(null!, [1, 2, 3], null));

    [Fact]
    public void Constructor_WithNullPayload_ThrowsArgumentNullException() {
        EventMetadata metadata = CreateMetadata();
        _ = Should.Throw<ArgumentNullException>(() => new EventEnvelope(metadata, null!, null));
    }

    [Fact]
    public void Extensions_DefensiveCopy_PreventsMutation() {
        var dict = new Dictionary<string, string> { ["key"] = "original" };
        var envelope = new EventEnvelope(CreateMetadata(), [1], dict);

        dict["key"] = "mutated";

        envelope.Extensions["key"].ShouldBe("original");
    }

    [Fact]
    public void Metadata_ExposesAll15Fields() {
        EventMetadata metadata = CreateMetadata();
        byte[] payload = [1, 2, 3];
        var envelope = new EventEnvelope(metadata, payload, null);

        envelope.Metadata.MessageId.ShouldBe("msg-001");
        envelope.Metadata.AggregateId.ShouldBe("order-123");
        envelope.Metadata.AggregateType.ShouldBe("order");
        envelope.Metadata.TenantId.ShouldBe("acme");
        envelope.Metadata.Domain.ShouldBe("payments");
        envelope.Metadata.SequenceNumber.ShouldBe(1);
        envelope.Metadata.GlobalPosition.ShouldBe(0);
        envelope.Metadata.Timestamp.ShouldNotBe(default);
        envelope.Metadata.CorrelationId.ShouldBe("corr-1");
        envelope.Metadata.CausationId.ShouldBe("cause-1");
        envelope.Metadata.UserId.ShouldBe("user-1");
        envelope.Metadata.DomainServiceVersion.ShouldBe("1.0.0");
        envelope.Metadata.EventTypeName.ShouldBe("OrderCreated");
        envelope.Metadata.MetadataVersion.ShouldBe(1);
        envelope.Metadata.SerializationFormat.ShouldBe("json");
    }

    /// <summary>
    /// Test ID: 1.1-UNIT-011. Closes Epic-1 R-T2 / TG-1 along the actual production serialization path.
    /// <para>
    /// <see cref="EventEnvelope"/> is intentionally not <c>[DataContract]</c>-decorated — it travels via
    /// <see cref="System.Text.Json.JsonSerializer"/> in DAPR pub/sub and cross-process projection flows
    /// (see <c>EventStoreAggregateTests.ProcessAsync_DaprEventEnvelopeFormat_*</c>). The actor-remoting
    /// DCS path uses the separate flat-record <c>Hexalith.EventStore.Server.Events.EventEnvelope</c>
    /// (covered by its own DCS round-trip test). This test pins that the envelope round-trips through
    /// <c>JsonSerializerDefaults.Web</c> with metadata (all 15 fields), payload bytes, and the
    /// <c>Extensions</c> dictionary all preserved.
    /// </para>
    /// </summary>
    [Fact]
    public void Json_SerializationRoundTrip_PreservesEnvelope() {
        var timestamp = new DateTimeOffset(2026, 5, 7, 12, 34, 56, TimeSpan.FromHours(2));
        var metadata = new EventMetadata(
            MessageId: "01HQK6Z0V8MFR3T1WKFB5J9YQX",
            AggregateId: "01HQK6Z0V8MFR3T1WKFB5J9AGG",
            AggregateType: "counter",
            TenantId: "acme",
            Domain: "billing",
            SequenceNumber: 42,
            GlobalPosition: 99_999,
            Timestamp: timestamp,
            CorrelationId: "01HQK6Z0V8MFR3T1WKFB5J9CRR",
            CausationId: "01HQK6Z0V8MFR3T1WKFB5J9CAU",
            UserId: "user-007",
            DomainServiceVersion: "1.4.2",
            EventTypeName: "Hexalith.Billing.Events.InvoiceIssued",
            MetadataVersion: 1,
            SerializationFormat: "json");
        byte[] payload = [0x01, 0x02, 0x03, 0xFE, 0xFF];
        var extensions = new Dictionary<string, string> {
            ["traceparent"] = "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01",
            ["custom"] = "value",
        };
        var original = new EventEnvelope(metadata, payload, extensions);
        var webOptions = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);

        byte[] serialized = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(original, webOptions);
        EventEnvelope deserialized = System.Text.Json.JsonSerializer.Deserialize<EventEnvelope>(serialized, webOptions)!;

        deserialized.Metadata.MessageId.ShouldBe(metadata.MessageId);
        deserialized.Metadata.AggregateId.ShouldBe(metadata.AggregateId);
        deserialized.Metadata.AggregateType.ShouldBe(metadata.AggregateType);
        deserialized.Metadata.TenantId.ShouldBe(metadata.TenantId);
        deserialized.Metadata.Domain.ShouldBe(metadata.Domain);
        deserialized.Metadata.SequenceNumber.ShouldBe(metadata.SequenceNumber);
        deserialized.Metadata.GlobalPosition.ShouldBe(metadata.GlobalPosition);
        deserialized.Metadata.Timestamp.ShouldBe(metadata.Timestamp);
        deserialized.Metadata.CorrelationId.ShouldBe(metadata.CorrelationId);
        deserialized.Metadata.CausationId.ShouldBe(metadata.CausationId);
        deserialized.Metadata.UserId.ShouldBe(metadata.UserId);
        deserialized.Metadata.DomainServiceVersion.ShouldBe(metadata.DomainServiceVersion);
        deserialized.Metadata.EventTypeName.ShouldBe(metadata.EventTypeName);
        deserialized.Metadata.MetadataVersion.ShouldBe(metadata.MetadataVersion);
        deserialized.Metadata.SerializationFormat.ShouldBe(metadata.SerializationFormat);

        deserialized.Payload.SequenceEqual(payload).ShouldBeTrue();

        deserialized.Extensions.Count.ShouldBe(2);
        deserialized.Extensions["traceparent"].ShouldBe(extensions["traceparent"]);
        deserialized.Extensions["custom"].ShouldBe(extensions["custom"]);
    }
}
