
using System.Reflection;

using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Contracts.Tests.Events;

public class EventMetadataTests {
    [Fact]
    public void Constructor_WithValidInputs_CreatesInstance() {
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;
        var metadata = new EventMetadata(
            AggregateId: "order-123",
            TenantId: "acme",
            Domain: "payments",
            SequenceNumber: 1,
            Timestamp: timestamp,
            CorrelationId: "corr-1",
            CausationId: "cause-1",
            UserId: "user-1",
            DomainServiceVersion: "1.0.0",
            EventTypeName: "OrderCreated",
            SerializationFormat: "json");

        Assert.Equal("order-123", metadata.AggregateId);
        Assert.Equal("acme", metadata.TenantId);
        Assert.Equal("payments", metadata.Domain);
        Assert.Equal(1, metadata.SequenceNumber);
        Assert.Equal(timestamp, metadata.Timestamp);
        Assert.Equal("corr-1", metadata.CorrelationId);
        Assert.Equal("cause-1", metadata.CausationId);
        Assert.Equal("user-1", metadata.UserId);
        Assert.Equal("1.0.0", metadata.DomainServiceVersion);
        Assert.Equal("OrderCreated", metadata.EventTypeName);
        Assert.Equal("json", metadata.SerializationFormat);
    }

    [Fact]
    public void EventMetadata_HasExactly11Fields() {
        PropertyInfo[] properties = typeof(EventMetadata).GetProperties();
        Assert.Equal(11, properties.Length);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(long.MinValue)]
    public void Constructor_WithSequenceNumberLessThan1_ThrowsArgumentOutOfRangeException(long sequenceNumber) => Assert.Throws<ArgumentOutOfRangeException>(() =>
            new EventMetadata("agg1", "t1", "d1", sequenceNumber, DateTimeOffset.UtcNow, "c1", "ca1", "u1", "v1", "e1", "json"));

    [Theory]
    [InlineData(1)]
    [InlineData(long.MaxValue)]
    public void Constructor_WithValidSequenceNumber_Succeeds(long sequenceNumber) {
        var metadata = new EventMetadata("agg1", "t1", "d1", sequenceNumber, DateTimeOffset.UtcNow, "c1", "ca1", "u1", "v1", "e1", "json");

        Assert.Equal(sequenceNumber, metadata.SequenceNumber);
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual() {
        var timestamp = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var m1 = new EventMetadata("agg1", "t1", "d1", 1, timestamp, "c1", "ca1", "u1", "v1", "e1", "json");
        var m2 = new EventMetadata("agg1", "t1", "d1", 1, timestamp, "c1", "ca1", "u1", "v1", "e1", "json");

        Assert.Equal(m1, m2);
    }
}
