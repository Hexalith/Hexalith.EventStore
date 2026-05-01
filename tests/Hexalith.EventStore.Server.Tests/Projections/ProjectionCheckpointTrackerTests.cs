using Dapr.Client;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Projections;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Projections;

public class ProjectionCheckpointTrackerTests {
    private static readonly AggregateIdentity TestIdentity = new("test-tenant", "test-domain", "agg-001");

    [Fact]
    public async Task ReadLastDeliveredSequenceAsync_MissingCheckpoint_ReturnsZero() {
        // Arrange
        DaprClient daprClient = Substitute.For<DaprClient>();
        var tracker = CreateTracker(daprClient);

        // Act
        long result = await tracker.ReadLastDeliveredSequenceAsync(TestIdentity);

        // Assert
        result.ShouldBe(0);
    }

    [Fact]
    public async Task ReadLastDeliveredSequenceAsync_ExistingCheckpoint_ReturnsStoredSequence() {
        // Arrange
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.GetStateAsync<ProjectionCheckpoint>(
                "statestore",
                ProjectionCheckpointTracker.GetStateKey(TestIdentity),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(new ProjectionCheckpoint("test-tenant", "test-domain", "agg-001", 42, DateTimeOffset.UtcNow));
        var tracker = CreateTracker(daprClient);

        // Act
        long result = await tracker.ReadLastDeliveredSequenceAsync(TestIdentity);

        // Assert
        result.ShouldBe(42);
    }

    [Fact]
    public async Task SaveDeliveredSequenceAsync_MissingCheckpoint_SavesDeliveredSequence() {
        // Arrange
        DaprClient daprClient = Substitute.For<DaprClient>();
        SetupGetStateAndEtag(daprClient, "custom-store", null, "etag-1");
        SetupTrySave(daprClient, "custom-store", true);
        var tracker = CreateTracker(daprClient, "custom-store");

        // Act
        bool saved = await tracker.SaveDeliveredSequenceAsync(TestIdentity, 9);

        // Assert
        saved.ShouldBeTrue();
        _ = await daprClient.Received(1).TrySaveStateAsync(
            "custom-store",
            ProjectionCheckpointTracker.GetStateKey(TestIdentity),
            Arg.Is<ProjectionCheckpoint>(p =>
                p.TenantId == "test-tenant"
                && p.Domain == "test-domain"
                && p.AggregateId == "agg-001"
                && p.LastDeliveredSequence == 9),
            "etag-1",
            stateOptions: Arg.Any<StateOptions?>(),
            metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveDeliveredSequenceAsync_ExistingHigherCheckpoint_KeepsMaximumSequence() {
        // Arrange
        DaprClient daprClient = Substitute.For<DaprClient>();
        SetupGetStateAndEtag(
            daprClient,
            "statestore",
            new ProjectionCheckpoint("test-tenant", "test-domain", "agg-001", 15, DateTimeOffset.UtcNow),
            "etag-2");
        SetupTrySave(daprClient, "statestore", true);
        var tracker = CreateTracker(daprClient);

        // Act
        bool saved = await tracker.SaveDeliveredSequenceAsync(TestIdentity, 8);

        // Assert
        saved.ShouldBeTrue();
        _ = await daprClient.Received(1).TrySaveStateAsync(
            "statestore",
            ProjectionCheckpointTracker.GetStateKey(TestIdentity),
            Arg.Is<ProjectionCheckpoint>(p => p.LastDeliveredSequence == 15),
            "etag-2",
            stateOptions: Arg.Any<StateOptions?>(),
            metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveDeliveredSequenceAsync_EtagMismatch_RetriesUntilSaveSucceeds() {
        // Arrange
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.GetStateAndETagAsync<ProjectionCheckpoint>(
                "statestore",
                ProjectionCheckpointTracker.GetStateKey(TestIdentity),
                consistencyMode: Arg.Any<ConsistencyMode?>(),
                metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(
                ((ProjectionCheckpoint, string))(null!, "etag-1"),
                ((ProjectionCheckpoint, string))(null!, "etag-2"));
        _ = daprClient.TrySaveStateAsync(
                "statestore",
                ProjectionCheckpointTracker.GetStateKey(TestIdentity),
                Arg.Any<ProjectionCheckpoint>(),
                Arg.Any<string>(),
                stateOptions: Arg.Any<StateOptions?>(),
                metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(false, true);
        var tracker = CreateTracker(daprClient);

        // Act
        bool saved = await tracker.SaveDeliveredSequenceAsync(TestIdentity, 11);

        // Assert
        saved.ShouldBeTrue();
        _ = await daprClient.Received(2).TrySaveStateAsync(
            "statestore",
            ProjectionCheckpointTracker.GetStateKey(TestIdentity),
            Arg.Any<ProjectionCheckpoint>(),
            Arg.Any<string>(),
            stateOptions: Arg.Any<StateOptions?>(),
            metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveDeliveredSequenceAsync_EtagRetryExhausted_ReturnsFalseWithoutBlindSave() {
        // Arrange
        DaprClient daprClient = Substitute.For<DaprClient>();
        SetupGetStateAndEtag(daprClient, "statestore", null, "etag-1");
        SetupTrySave(daprClient, "statestore", false);
        var tracker = CreateTracker(daprClient);

        // Act
        bool saved = await tracker.SaveDeliveredSequenceAsync(TestIdentity, 11);

        // Assert
        saved.ShouldBeFalse();
        _ = await daprClient.Received(3).TrySaveStateAsync(
            "statestore",
            ProjectionCheckpointTracker.GetStateKey(TestIdentity),
            Arg.Any<ProjectionCheckpoint>(),
            Arg.Any<string>(),
            stateOptions: Arg.Any<StateOptions?>(),
            metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
            cancellationToken: Arg.Any<CancellationToken>());
        await daprClient.DidNotReceiveWithAnyArgs().SaveStateAsync(default!, default!, Arg.Any<ProjectionCheckpoint>());
    }

    [Fact]
    public void GetStateKey_UsesDedicatedProjectionCheckpointNamespace() {
        // Act
        string key = ProjectionCheckpointTracker.GetStateKey(TestIdentity);

        // Assert
        key.ShouldBe("projection-checkpoints:test-tenant:test-domain:agg-001");
        key.ShouldNotContain(":events:");
        key.ShouldNotEndWith(":metadata");
    }

    [Fact]
    public async Task SaveDeliveredSequenceAsync_NegativeSequence_ThrowsArgumentOutOfRangeException() {
        // Arrange
        var tracker = CreateTracker(Substitute.For<DaprClient>());

        // Act & Assert
        _ = await Should.ThrowAsync<ArgumentOutOfRangeException>(() => tracker.SaveDeliveredSequenceAsync(TestIdentity, -1));
    }

    [Fact]
    public async Task ReadLastDeliveredSequenceAsync_StorageThrows_PropagatesException() {
        // Arrange
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.GetStateAsync<ProjectionCheckpoint>(
                "statestore",
                ProjectionCheckpointTracker.GetStateKey(TestIdentity),
                consistencyMode: Arg.Any<ConsistencyMode?>(),
                metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(Task.FromException<ProjectionCheckpoint>(new InvalidOperationException("state store unavailable")));
        var tracker = CreateTracker(daprClient);

        // Act & Assert -- tracker does not catch storage exceptions; orchestrator's outer catch handles them.
        _ = await Should.ThrowAsync<InvalidOperationException>(() => tracker.ReadLastDeliveredSequenceAsync(TestIdentity));
    }

    [Fact]
    public async Task SaveDeliveredSequenceAsync_StorageThrowsOnLastRetry_PropagatesException() {
        // Arrange -- mirrors the precedent shape in DaprCommandActivityTracker:
        // intermediate retries are swallowed, but the final-attempt exception surfaces
        // so persistent storage errors are not hidden behind a Debug-only log trail.
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.GetStateAndETagAsync<ProjectionCheckpoint>(
                "statestore",
                ProjectionCheckpointTracker.GetStateKey(TestIdentity),
                consistencyMode: Arg.Any<ConsistencyMode?>(),
                metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(Task.FromException<(ProjectionCheckpoint, string)>(new InvalidOperationException("state store unavailable")));
        var tracker = CreateTracker(daprClient);

        // Act & Assert
        _ = await Should.ThrowAsync<InvalidOperationException>(() => tracker.SaveDeliveredSequenceAsync(TestIdentity, 7));
    }

    [Fact]
    public async Task SaveDeliveredSequenceAsync_GetStateAndEtagThrowsOperationCanceledException_Propagates() {
        // Arrange -- guard against future refactors that would absorb cancellation in the catch-all.
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.GetStateAndETagAsync<ProjectionCheckpoint>(
                "statestore",
                ProjectionCheckpointTracker.GetStateKey(TestIdentity),
                consistencyMode: Arg.Any<ConsistencyMode?>(),
                metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(Task.FromException<(ProjectionCheckpoint, string)>(new OperationCanceledException()));
        var tracker = CreateTracker(daprClient);

        // Act & Assert
        _ = await Should.ThrowAsync<OperationCanceledException>(() => tracker.SaveDeliveredSequenceAsync(TestIdentity, 7));
    }

    private static ProjectionCheckpointTracker CreateTracker(DaprClient daprClient, string stateStoreName = "statestore") =>
        new(daprClient, Options.Create(new ProjectionOptions { CheckpointStateStoreName = stateStoreName }), NullLogger<ProjectionCheckpointTracker>.Instance);

    private static void SetupGetStateAndEtag(
        DaprClient daprClient,
        string stateStoreName,
        ProjectionCheckpoint? value,
        string etag) => _ = daprClient.GetStateAndETagAsync<ProjectionCheckpoint>(
            stateStoreName,
            ProjectionCheckpointTracker.GetStateKey(TestIdentity),
            consistencyMode: Arg.Any<ConsistencyMode?>(),
            metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(((ProjectionCheckpoint, string))(value!, etag));

    private static void SetupTrySave(DaprClient daprClient, string stateStoreName, bool result) => _ = daprClient.TrySaveStateAsync(
            stateStoreName,
            ProjectionCheckpointTracker.GetStateKey(TestIdentity),
            Arg.Any<ProjectionCheckpoint>(),
            Arg.Any<string>(),
            stateOptions: Arg.Any<StateOptions?>(),
            metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
            cancellationToken: Arg.Any<CancellationToken>())
        .Returns(result);
}
