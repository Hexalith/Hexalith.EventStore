
using System.Reflection;

using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Contracts.Tests.Events;

public class EventMetadataTests {
    [Fact]
    public void Constructor_WithValidInputs_CreatesInstance() {
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;
        var metadata = new EventMetadata(
            MessageId: "msg-001",
            AggregateId: "order-123",
            AggregateType: "order",
            TenantId: "acme",
            Domain: "payments",
            SequenceNumber: 1,
            GlobalPosition: 0,
            Timestamp: timestamp,
            CorrelationId: "corr-1",
            CausationId: "cause-1",
            UserId: "user-1",
            DomainServiceVersion: "1.0.0",
            EventTypeName: "OrderCreated",
            MetadataVersion: 1,
            SerializationFormat: "json");

        metadata.MessageId.ShouldBe("msg-001");
        metadata.AggregateId.ShouldBe("order-123");
        metadata.AggregateType.ShouldBe("order");
        metadata.TenantId.ShouldBe("acme");
        metadata.Domain.ShouldBe("payments");
        metadata.SequenceNumber.ShouldBe(1);
        metadata.GlobalPosition.ShouldBe(0);
        metadata.Timestamp.ShouldBe(timestamp);
        metadata.CorrelationId.ShouldBe("corr-1");
        metadata.CausationId.ShouldBe("cause-1");
        metadata.UserId.ShouldBe("user-1");
        metadata.DomainServiceVersion.ShouldBe("1.0.0");
        metadata.EventTypeName.ShouldBe("OrderCreated");
        metadata.MetadataVersion.ShouldBe(1);
        metadata.SerializationFormat.ShouldBe("json");
    }

    [Fact]
    public void EventMetadata_HasExactly15Fields() {
        PropertyInfo[] properties = typeof(EventMetadata).GetProperties();
        properties.Length.ShouldBe(15);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(long.MinValue)]
    public void Constructor_WithSequenceNumberLessThan1_ThrowsArgumentOutOfRangeException(long sequenceNumber) => Should.Throw<ArgumentOutOfRangeException>(() =>
            new EventMetadata("msg1", "agg1", "agg-type", "t1", "d1", sequenceNumber, 0, DateTimeOffset.UtcNow, "c1", "ca1", "u1", "v1", "e1", 1, "json"));

    [Theory]
    [InlineData(1)]
    [InlineData(long.MaxValue)]
    public void Constructor_WithValidSequenceNumber_Succeeds(long sequenceNumber) {
        var metadata = new EventMetadata("msg1", "agg1", "agg-type", "t1", "d1", sequenceNumber, 0, DateTimeOffset.UtcNow, "c1", "ca1", "u1", "v1", "e1", 1, "json");

        metadata.SequenceNumber.ShouldBe(sequenceNumber);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-100)]
    [InlineData(long.MinValue)]
    public void Constructor_WithGlobalPositionLessThan0_ThrowsArgumentOutOfRangeException(long globalPosition) => Should.Throw<ArgumentOutOfRangeException>(() =>
            new EventMetadata("msg1", "agg1", "agg-type", "t1", "d1", 1, globalPosition, DateTimeOffset.UtcNow, "c1", "ca1", "u1", "v1", "e1", 1, "json"));

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(long.MaxValue)]
    public void Constructor_WithValidGlobalPosition_Succeeds(long globalPosition) {
        var metadata = new EventMetadata("msg1", "agg1", "agg-type", "t1", "d1", 1, globalPosition, DateTimeOffset.UtcNow, "c1", "ca1", "u1", "v1", "e1", 1, "json");

        metadata.GlobalPosition.ShouldBe(globalPosition);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Constructor_WithMetadataVersionLessThan1_ThrowsArgumentOutOfRangeException(int metadataVersion) => Should.Throw<ArgumentOutOfRangeException>(() =>
            new EventMetadata("msg1", "agg1", "agg-type", "t1", "d1", 1, 0, DateTimeOffset.UtcNow, "c1", "ca1", "u1", "v1", "e1", metadataVersion, "json"));

    [Theory]
    [InlineData(1)]
    [InlineData(int.MaxValue)]
    public void Constructor_WithValidMetadataVersion_Succeeds(int metadataVersion) {
        var metadata = new EventMetadata("msg1", "agg1", "agg-type", "t1", "d1", 1, 0, DateTimeOffset.UtcNow, "c1", "ca1", "u1", "v1", "e1", metadataVersion, "json");

        metadata.MetadataVersion.ShouldBe(metadataVersion);
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual() {
        var timestamp = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var m1 = new EventMetadata("msg1", "agg1", "agg-type", "t1", "d1", 1, 0, timestamp, "c1", "ca1", "u1", "v1", "e1", 1, "json");
        var m2 = new EventMetadata("msg1", "agg1", "agg-type", "t1", "d1", 1, 0, timestamp, "c1", "ca1", "u1", "v1", "e1", 1, "json");

        m2.ShouldBe(m1);
    }

    /// <summary>
    /// Test ID: 1.1-UNIT-010. Closes Epic-1 R-T2 / TG-1 along the actual production serialization path.
    /// <para>
    /// <see cref="EventMetadata"/> is intentionally not <c>[DataContract]</c>-decorated — it travels via
    /// <see cref="System.Text.Json.JsonSerializer"/> (DAPR pub/sub, cross-process projection state, the
    /// Dapr <c>EventEnvelope</c> wire format used by <c>EventStoreAggregate</c>). The actor-remoting DCS
    /// path uses the separate flat-record <c>Hexalith.EventStore.Server.Events.EventEnvelope</c>
    /// (covered by the Server-side DCS round-trip test). This test pins that all 15 fields survive a
    /// <c>JsonSerializerDefaults.Web</c> round-trip — the camelCase-property convention DAPR uses.
    /// If a future refactor adds, removes, or reorders a positional parameter without keeping the
    /// runtime contract intact, this test fails immediately rather than at first projection replay.
    /// </para>
    /// </summary>
    [Fact]
    public void Json_SerializationRoundTrip_PreservesAll15Fields() {
        var timestamp = new DateTimeOffset(2026, 5, 7, 12, 34, 56, TimeSpan.FromHours(2));
        var original = new EventMetadata(
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
        var webOptions = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);

        byte[] serialized = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(original, webOptions);
        EventMetadata deserialized = System.Text.Json.JsonSerializer.Deserialize<EventMetadata>(serialized, webOptions)!;

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
    }
}
