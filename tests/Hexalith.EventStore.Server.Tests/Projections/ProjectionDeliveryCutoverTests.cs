using Hexalith.EventStore.Server.Projections;

using Microsoft.Extensions.Time.Testing;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Projections;

public class ProjectionDeliveryCutoverTests {
    [Fact]
    public async Task Activate_RequiresBackupQuiescenceAndNoDowngradeAttestations() {
        IProjectionDeliveryStateStore store = Substitute.For<IProjectionDeliveryStateStore>();
        var cutover = new ProjectionDeliveryCutover(store, new FakeTimeProvider(DateTimeOffset.UtcNow));

        ProjectionDeliveryCutoverStatus result = await cutover.ActivateAsync(
            new ProjectionDeliveryCutoverRequest("commit", "backup", true, false, true));

        result.ShouldBe(ProjectionDeliveryCutoverStatus.PreconditionsFailed);
        _ = await store.DidNotReceiveWithAnyArgs().ReadWriterProtocolAsync(default);
    }

    [Fact]
    public async Task Activate_FirstWritesExactCommitAfterAllAttestations() {
        IProjectionDeliveryStateStore store = Substitute.For<IProjectionDeliveryStateStore>();
        _ = store.ReadWriterProtocolAsync(Arg.Any<CancellationToken>())
            .Returns((ProjectionDeliveryWriterProtocol?)null);
        _ = store.TryActivateWriterProtocolAsync(Arg.Any<ProjectionDeliveryWriterProtocol>(), Arg.Any<CancellationToken>())
            .Returns(true);
        DateTimeOffset now = new(2026, 7, 14, 10, 0, 0, TimeSpan.Zero);
        var cutover = new ProjectionDeliveryCutover(store, new FakeTimeProvider(now));

        ProjectionDeliveryCutoverStatus result = await cutover.ActivateAsync(
            new ProjectionDeliveryCutoverRequest("commit-abc", "backup-20260714", true, true, true));

        result.ShouldBe(ProjectionDeliveryCutoverStatus.Activated);
        _ = await store.Received(1).TryActivateWriterProtocolAsync(
            Arg.Is<ProjectionDeliveryWriterProtocol>(marker =>
                marker.CutoverCommit == "commit-abc" && marker.ActivatedAt == now),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Activate_ConcurrentFirstWriteOfSameCommit_IsIdempotentlyActivated() {
        IProjectionDeliveryStateStore store = Substitute.For<IProjectionDeliveryStateStore>();
        var marker = new ProjectionDeliveryWriterProtocol(
            ProjectionDeliveryWriterProtocol.CurrentSchemaVersion,
            ProjectionDeliveryWriterProtocol.CurrentWriterProtocolVersion,
            "commit-abc",
            DateTimeOffset.UtcNow);
        _ = store.ReadWriterProtocolAsync(Arg.Any<CancellationToken>())
            .Returns((ProjectionDeliveryWriterProtocol?)null, marker);
        _ = store.TryActivateWriterProtocolAsync(Arg.Any<ProjectionDeliveryWriterProtocol>(), Arg.Any<CancellationToken>())
            .Returns(false);
        var cutover = new ProjectionDeliveryCutover(store, new FakeTimeProvider(DateTimeOffset.UtcNow));

        ProjectionDeliveryCutoverStatus result = await cutover.ActivateAsync(
            new ProjectionDeliveryCutoverRequest("commit-abc", "backup", true, true, true));

        result.ShouldBe(ProjectionDeliveryCutoverStatus.Activated);
        _ = await store.Received(2).ReadWriterProtocolAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Activate_ExistingDifferentCommit_IsConflictWithoutMutation() {
        IProjectionDeliveryStateStore store = Substitute.For<IProjectionDeliveryStateStore>();
        _ = store.ReadWriterProtocolAsync(Arg.Any<CancellationToken>())
            .Returns(new ProjectionDeliveryWriterProtocol(
                ProjectionDeliveryWriterProtocol.CurrentSchemaVersion,
                ProjectionDeliveryWriterProtocol.CurrentWriterProtocolVersion,
                "commit-other",
                DateTimeOffset.UtcNow));
        var cutover = new ProjectionDeliveryCutover(store, new FakeTimeProvider(DateTimeOffset.UtcNow));

        ProjectionDeliveryCutoverStatus result = await cutover.ActivateAsync(
            new ProjectionDeliveryCutoverRequest("commit-abc", "backup", true, true, true));

        result.ShouldBe(ProjectionDeliveryCutoverStatus.Conflict);
        _ = await store.DidNotReceiveWithAnyArgs().TryActivateWriterProtocolAsync(default!, default);
    }

    [Fact]
    public async Task Activate_ConcurrentFirstWriteOfDifferentCommit_IsConflict() {
        IProjectionDeliveryStateStore store = Substitute.For<IProjectionDeliveryStateStore>();
        var conflicting = new ProjectionDeliveryWriterProtocol(
            ProjectionDeliveryWriterProtocol.CurrentSchemaVersion,
            ProjectionDeliveryWriterProtocol.CurrentWriterProtocolVersion,
            "commit-other",
            DateTimeOffset.UtcNow);
        _ = store.ReadWriterProtocolAsync(Arg.Any<CancellationToken>())
            .Returns((ProjectionDeliveryWriterProtocol?)null, conflicting);
        _ = store.TryActivateWriterProtocolAsync(Arg.Any<ProjectionDeliveryWriterProtocol>(), Arg.Any<CancellationToken>())
            .Returns(false);
        var cutover = new ProjectionDeliveryCutover(store, new FakeTimeProvider(DateTimeOffset.UtcNow));

        ProjectionDeliveryCutoverStatus result = await cutover.ActivateAsync(
            new ProjectionDeliveryCutoverRequest("commit-abc", "backup", true, true, true));

        result.ShouldBe(ProjectionDeliveryCutoverStatus.Conflict);
        _ = await store.Received(2).ReadWriterProtocolAsync(Arg.Any<CancellationToken>());
    }
}
