using Hexalith.EventStore.Admin.Abstractions.Models.Storage;

namespace Hexalith.EventStore.Admin.Abstractions.Tests.Models.Storage;

public class SnapshotJobStatusTests {
    [Theory]
    [InlineData(SnapshotJobStatus.Queued)]
    [InlineData(SnapshotJobStatus.Running)]
    [InlineData(SnapshotJobStatus.Done)]
    [InlineData(SnapshotJobStatus.AlreadyCurrent)]
    [InlineData(SnapshotJobStatus.Failed)]
    public void EnumValue_IsDefined(SnapshotJobStatus status)
        => Enum.IsDefined(status).ShouldBeTrue();

    [Fact]
    public void EnumValues_AreOrderedQueuedRunningDoneAlreadyCurrentFailed() {
        SnapshotJobStatus[] values = Enum.GetValues<SnapshotJobStatus>();
        values.ShouldBe([
            SnapshotJobStatus.Queued,
            SnapshotJobStatus.Running,
            SnapshotJobStatus.Done,
            SnapshotJobStatus.AlreadyCurrent,
            SnapshotJobStatus.Failed,
        ]);
    }
}
