namespace Hexalith.EventStore.Server.Tests.Events;

using Hexalith.EventStore.Server.Events;

using Shouldly;

public class SnapshotRecordTests
{
    // === 9.1: SnapshotRecord created with correct properties ===

    [Fact]
    public void SnapshotRecord_CreatedWithCorrectProperties()
    {
        var state = new { Counter = 42 };
        var createdAt = DateTimeOffset.UtcNow;

        var record = new SnapshotRecord(
            SequenceNumber: 100,
            State: state,
            CreatedAt: createdAt,
            Domain: "orders",
            AggregateId: "order-001",
            TenantId: "tenant-a");

        record.SequenceNumber.ShouldBe(100);
        record.State.ShouldBe(state);
        record.CreatedAt.ShouldBe(createdAt);
        record.Domain.ShouldBe("orders");
        record.AggregateId.ShouldBe("order-001");
        record.TenantId.ShouldBe("tenant-a");
    }

    // === 9.2: SnapshotRecord is immutable (record type guarantees) ===

    [Fact]
    public void SnapshotRecord_IsImmutable_RecordTypeGuarantees()
    {
        var record = new SnapshotRecord(100, "state", DateTimeOffset.UtcNow, "domain", "agg-1", "tenant-1");

        // Record types use value equality
        var copy = record with { SequenceNumber = 200 };

        copy.SequenceNumber.ShouldBe(200);
        record.SequenceNumber.ShouldBe(100); // Original unchanged
        record.ShouldNotBe(copy); // Different values → not equal
    }

    [Fact]
    public void SnapshotRecord_ValueEquality()
    {
        var createdAt = DateTimeOffset.UtcNow;
        var state = "same-state";

        var record1 = new SnapshotRecord(100, state, createdAt, "domain", "agg-1", "tenant-1");
        var record2 = new SnapshotRecord(100, state, createdAt, "domain", "agg-1", "tenant-1");

        record1.ShouldBe(record2);
    }
}
