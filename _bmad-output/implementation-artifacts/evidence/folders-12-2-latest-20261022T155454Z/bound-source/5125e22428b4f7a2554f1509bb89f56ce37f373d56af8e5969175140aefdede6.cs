using Hexalith.EventStore.Admin.Abstractions.Models.Storage;

namespace Hexalith.EventStore.Admin.Abstractions.Tests.Models.Storage;

public class SnapshotJobTests {
    [Fact]
    public void Constructor_WithTerminalDoneStatus_AssignsAllFields() {
        DateTimeOffset started = DateTimeOffset.UtcNow.AddSeconds(-1);
        DateTimeOffset completed = DateTimeOffset.UtcNow;

        var job = new SnapshotJob(
            OperationId: "manual-snapshot-abc",
            TenantId: "tenant-a",
            Domain: "counter",
            AggregateId: "counter-1",
            SequenceNumber: 42,
            Status: SnapshotJobStatus.Done,
            StartedAtUtc: started,
            CompletedAtUtc: completed,
            SnapshotKey: "tenant-a:counter:counter-1:snapshot",
            ErrorCode: null,
            ErrorMessage: null);

        job.OperationId.ShouldBe("manual-snapshot-abc");
        job.TenantId.ShouldBe("tenant-a");
        job.Domain.ShouldBe("counter");
        job.AggregateId.ShouldBe("counter-1");
        job.SequenceNumber.ShouldBe(42);
        job.Status.ShouldBe(SnapshotJobStatus.Done);
        job.StartedAtUtc.ShouldBe(started);
        job.CompletedAtUtc.ShouldBe(completed);
        job.SnapshotKey.ShouldBe("tenant-a:counter:counter-1:snapshot");
        job.ErrorCode.ShouldBeNull();
        job.ErrorMessage.ShouldBeNull();
    }

    [Fact]
    public void Constructor_WithAlreadyCurrentStatus_AssignsAllFields() {
        var job = new SnapshotJob(
            "manual-snapshot-xyz",
            "tenant-b",
            "orders",
            "order-1",
            7,
            SnapshotJobStatus.AlreadyCurrent,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            "tenant-b:orders:order-1:snapshot",
            null,
            null);

        job.Status.ShouldBe(SnapshotJobStatus.AlreadyCurrent);
    }

    [Fact]
    public void Constructor_WithFailedStatus_AssignsErrorFields() {
        var job = new SnapshotJob(
            "manual-snapshot-fail",
            "tenant-c",
            "orders",
            "order-2",
            10,
            SnapshotJobStatus.Failed,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            null,
            "UnreadableProtected",
            "Existing snapshot cannot be safely read.");

        job.Status.ShouldBe(SnapshotJobStatus.Failed);
        job.SnapshotKey.ShouldBeNull();
        job.ErrorCode.ShouldBe("UnreadableProtected");
        job.ErrorMessage.ShouldBe("Existing snapshot cannot be safely read.");
    }

    [Fact]
    public void Constructor_WithRunningStatus_AllowsNullCompletedAt() {
        var job = new SnapshotJob(
            "manual-snapshot-run",
            "tenant-d",
            "counter",
            "counter-9",
            5,
            SnapshotJobStatus.Running,
            DateTimeOffset.UtcNow,
            null,
            null,
            null,
            null);

        job.Status.ShouldBe(SnapshotJobStatus.Running);
        job.CompletedAtUtc.ShouldBeNull();
    }

    [Fact]
    public void RecordEquality_IdenticalValues_AreEqual() {
        DateTimeOffset t = DateTimeOffset.UtcNow;
        var a = new SnapshotJob("op-1", "t", "d", "a", 1, SnapshotJobStatus.Done, t, t, "k", null, null);
        var b = new SnapshotJob("op-1", "t", "d", "a", 1, SnapshotJobStatus.Done, t, t, "k", null, null);

        a.ShouldBe(b);
    }
}
