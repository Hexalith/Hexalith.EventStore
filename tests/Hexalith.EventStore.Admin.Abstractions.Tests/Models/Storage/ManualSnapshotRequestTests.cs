using Hexalith.EventStore.Admin.Abstractions.Models.Storage;

namespace Hexalith.EventStore.Admin.Abstractions.Tests.Models.Storage;

public class ManualSnapshotRequestTests {
    [Fact]
    public void Constructor_AssignsAllFields() {
        var request = new ManualSnapshotRequest("tenant-a", "Counter", "counter-1", "corr-1");

        request.TenantId.ShouldBe("tenant-a");
        request.Domain.ShouldBe("Counter");
        request.AggregateId.ShouldBe("counter-1");
        request.CorrelationId.ShouldBe("corr-1");
    }

    [Fact]
    public void Constructor_CorrelationId_DefaultsToNull() {
        var request = new ManualSnapshotRequest("tenant-a", "Counter", "counter-1");

        request.CorrelationId.ShouldBeNull();
    }

    [Fact]
    public void RecordEquality_IdenticalValues_AreEqual() {
        var a = new ManualSnapshotRequest("t", "d", "a", "c");
        var b = new ManualSnapshotRequest("t", "d", "a", "c");

        a.ShouldBe(b);
    }
}
