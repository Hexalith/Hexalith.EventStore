
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

        Assert.Equal("msg-001", metadata.MessageId);
        Assert.Equal("order-123", metadata.AggregateId);
        Assert.Equal("order", metadata.AggregateType);
        Assert.Equal("acme", metadata.TenantId);
        Assert.Equal("payments", metadata.Domain);
        Assert.Equal(1, metadata.SequenceNumber);
        Assert.Equal(0, metadata.GlobalPosition);
        Assert.Equal(timestamp, metadata.Timestamp);
        Assert.Equal("corr-1", metadata.CorrelationId);
        Assert.Equal("cause-1", metadata.CausationId);
        Assert.Equal("user-1", metadata.UserId);
        Assert.Equal("1.0.0", metadata.DomainServiceVersion);
        Assert.Equal("OrderCreated", metadata.EventTypeName);
        Assert.Equal(1, metadata.MetadataVersion);
        Assert.Equal("json", metadata.SerializationFormat);
    }

    [Fact]
    public void EventMetadata_HasExactly15Fields() {
        PropertyInfo[] properties = typeof(EventMetadata).GetProperties();
        Assert.Equal(15, properties.Length);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(long.MinValue)]
    public void Constructor_WithSequenceNumberLessThan1_ThrowsArgumentOutOfRangeException(long sequenceNumber) => Assert.Throws<ArgumentOutOfRangeException>(() =>
            new EventMetadata("msg1", "agg1", "agg-type", "t1", "d1", sequenceNumber, 0, DateTimeOffset.UtcNow, "c1", "ca1", "u1", "v1", "e1", 1, "json"));

    [Theory]
    [InlineData(1)]
    [InlineData(long.MaxValue)]
    public void Constructor_WithValidSequenceNumber_Succeeds(long sequenceNumber) {
        var metadata = new EventMetadata("msg1", "agg1", "agg-type", "t1", "d1", sequenceNumber, 0, DateTimeOffset.UtcNow, "c1", "ca1", "u1", "v1", "e1", 1, "json");

        Assert.Equal(sequenceNumber, metadata.SequenceNumber);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-100)]
    [InlineData(long.MinValue)]
    public void Constructor_WithGlobalPositionLessThan0_ThrowsArgumentOutOfRangeException(long globalPosition) => Assert.Throws<ArgumentOutOfRangeException>(() =>
            new EventMetadata("msg1", "agg1", "agg-type", "t1", "d1", 1, globalPosition, DateTimeOffset.UtcNow, "c1", "ca1", "u1", "v1", "e1", 1, "json"));

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(long.MaxValue)]
    public void Constructor_WithValidGlobalPosition_Succeeds(long globalPosition) {
        var metadata = new EventMetadata("msg1", "agg1", "agg-type", "t1", "d1", 1, globalPosition, DateTimeOffset.UtcNow, "c1", "ca1", "u1", "v1", "e1", 1, "json");

        Assert.Equal(globalPosition, metadata.GlobalPosition);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Constructor_WithMetadataVersionLessThan1_ThrowsArgumentOutOfRangeException(int metadataVersion) => Assert.Throws<ArgumentOutOfRangeException>(() =>
            new EventMetadata("msg1", "agg1", "agg-type", "t1", "d1", 1, 0, DateTimeOffset.UtcNow, "c1", "ca1", "u1", "v1", "e1", metadataVersion, "json"));

    [Theory]
    [InlineData(1)]
    [InlineData(int.MaxValue)]
    public void Constructor_WithValidMetadataVersion_Succeeds(int metadataVersion) {
        var metadata = new EventMetadata("msg1", "agg1", "agg-type", "t1", "d1", 1, 0, DateTimeOffset.UtcNow, "c1", "ca1", "u1", "v1", "e1", metadataVersion, "json");

        Assert.Equal(metadataVersion, metadata.MetadataVersion);
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual() {
        var timestamp = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var m1 = new EventMetadata("msg1", "agg1", "agg-type", "t1", "d1", 1, 0, timestamp, "c1", "ca1", "u1", "v1", "e1", 1, "json");
        var m2 = new EventMetadata("msg1", "agg1", "agg-type", "t1", "d1", 1, 0, timestamp, "c1", "ca1", "u1", "v1", "e1", 1, "json");

        Assert.Equal(m1, m2);
    }
}
