
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
}
