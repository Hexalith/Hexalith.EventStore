using System.Linq;

using Dapr.Client;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Projections;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Projections;

/// <summary>
/// Story 1.9 Task 3: projection-scoped delivery checkpoint store (<see cref="IProjectionDeliveryCheckpointStore"/>),
/// implemented by the same <see cref="ProjectionCheckpointTracker"/> singleton, with lazy per-projection
/// migration off the legacy aggregate-wide checkpoint (Option A).
/// </summary>
public class ProjectionDeliveryCheckpointStoreTests {
    private const string ProjectionName = "counter-summary";
    private static readonly AggregateIdentity TestIdentity = new("test-tenant", "test-domain", "agg-001");

    [Fact]
    public async Task ReadDeliveredSequenceAsync_UsesProjectionScopedKey() {
        // Arrange
        DaprClient daprClient = Substitute.For<DaprClient>();
        string scopedKey = ProjectionCheckpointTracker.GetProjectionScopedStateKey(TestIdentity, ProjectionName);
        _ = daprClient.GetStateAsync<ProjectionCheckpoint>(
                "statestore",
                scopedKey,
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(new ProjectionCheckpoint("test-tenant", "test-domain", "agg-001", 5, DateTimeOffset.UtcNow));
        ProjectionCheckpointTracker tracker = CreateTracker(daprClient);

        // Act
        long result = await tracker.ReadDeliveredSequenceAsync(TestIdentity, ProjectionName);

        // Assert — key composition is {prefix}{tenant}:{domain}:{agg}:{projection}.
        result.ShouldBe(5);
        scopedKey.ShouldBe("projection-checkpoints:test-tenant:test-domain:agg-001:counter-summary");
        _ = await daprClient.Received(1).GetStateAsync<ProjectionCheckpoint>(
            "statestore",
            scopedKey,
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReadDeliveredSequenceAsync_ScopedMismatchedIdentity_ThrowsInvalidOperationException() {
        // Arrange — a scoped checkpoint that belongs to a different tenant must be rejected, mirroring
        // the released aggregate-wide read.
        DaprClient daprClient = Substitute.For<DaprClient>();
        string scopedKey = ProjectionCheckpointTracker.GetProjectionScopedStateKey(TestIdentity, ProjectionName);
        _ = daprClient.GetStateAsync<ProjectionCheckpoint>(
                "statestore",
                scopedKey,
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(new ProjectionCheckpoint("other-tenant", "test-domain", "agg-001", 5, DateTimeOffset.UtcNow));
        ProjectionCheckpointTracker tracker = CreateTracker(daprClient);

        // Act & Assert
        _ = await Should.ThrowAsync<InvalidOperationException>(() => tracker.ReadDeliveredSequenceAsync(TestIdentity, ProjectionName));
    }

    [Fact]
    public async Task ReadDeliveredSequenceAsync_ScopedAbsentLegacyPresent_MigratesAndSeedsScopedKey() {
        // Arrange — first read for this projection: scoped key absent, no migration marker, legacy
        // aggregate-wide checkpoint holds a high-water mark of 9.
        DaprClient daprClient = Substitute.For<DaprClient>();
        string scopedKey = ProjectionCheckpointTracker.GetProjectionScopedStateKey(TestIdentity, ProjectionName);
        string markerKey = ProjectionCheckpointTracker.GetMigratedMarkerKey(TestIdentity, ProjectionName);
        string legacyKey = ProjectionCheckpointTracker.GetStateKey(TestIdentity);

        _ = daprClient.GetStateAsync<ProjectionCheckpoint>("statestore", scopedKey, cancellationToken: Arg.Any<CancellationToken>())
            .Returns((ProjectionCheckpoint?)null);
        _ = daprClient.GetStateAsync<ProjectionCheckpointTracker.ProjectionCheckpointMigrationMarker>("statestore", markerKey, cancellationToken: Arg.Any<CancellationToken>())
            .Returns((ProjectionCheckpointTracker.ProjectionCheckpointMigrationMarker?)null);
        _ = daprClient.GetStateAsync<ProjectionCheckpoint>("statestore", legacyKey, cancellationToken: Arg.Any<CancellationToken>())
            .Returns(new ProjectionCheckpoint("test-tenant", "test-domain", "agg-001", 9, DateTimeOffset.UtcNow));
        SetupGetStateAndEtag(daprClient, scopedKey, null, string.Empty);
        _ = daprClient.TrySaveStateAsync(
                "statestore",
                scopedKey,
                Arg.Any<ProjectionCheckpoint>(),
                string.Empty,
                stateOptions: Arg.Any<StateOptions?>(),
                metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(true);
        ProjectionCheckpointTracker tracker = CreateTracker(daprClient);

        // Act
        long result = await tracker.ReadDeliveredSequenceAsync(TestIdentity, ProjectionName);

        // Assert — returns the legacy value AND seeds the scoped key + persists the marker.
        result.ShouldBe(9);
        _ = await daprClient.Received(1).TrySaveStateAsync(
            "statestore",
            scopedKey,
            Arg.Is<ProjectionCheckpoint>(p =>
                p.TenantId == "test-tenant"
                && p.Domain == "test-domain"
                && p.AggregateId == "agg-001"
                && p.LastDeliveredSequence == 9),
            string.Empty,
            stateOptions: Arg.Any<StateOptions?>(),
            metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
            cancellationToken: Arg.Any<CancellationToken>());
        await daprClient.Received(1).SaveStateAsync(
            "statestore",
            markerKey,
            Arg.Any<ProjectionCheckpointTracker.ProjectionCheckpointMigrationMarker>(),
            stateOptions: Arg.Any<StateOptions?>(),
            metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
            cancellationToken: Arg.Any<CancellationToken>());
        // The legacy key is NEVER deleted.
        _ = await daprClient.DidNotReceiveWithAnyArgs().TryDeleteStateAsync(
            default!,
            default!,
            default!,
            stateOptions: default,
            metadata: default,
            cancellationToken: default);
    }

    [Fact]
    public async Task ReadDeliveredSequenceAsync_ScopedAbsentButMarkerPresent_ReturnsZeroWithoutLegacyFallback() {
        // Arrange — post-erasure state: scoped key absent, migration marker present, legacy still holds 9.
        // A read must return 0 (the erase intent), NOT re-migrate the legacy value.
        DaprClient daprClient = Substitute.For<DaprClient>();
        string scopedKey = ProjectionCheckpointTracker.GetProjectionScopedStateKey(TestIdentity, ProjectionName);
        string markerKey = ProjectionCheckpointTracker.GetMigratedMarkerKey(TestIdentity, ProjectionName);
        string legacyKey = ProjectionCheckpointTracker.GetStateKey(TestIdentity);

        _ = daprClient.GetStateAsync<ProjectionCheckpoint>("statestore", scopedKey, cancellationToken: Arg.Any<CancellationToken>())
            .Returns((ProjectionCheckpoint?)null);
        _ = daprClient.GetStateAsync<ProjectionCheckpointTracker.ProjectionCheckpointMigrationMarker>("statestore", markerKey, cancellationToken: Arg.Any<CancellationToken>())
            .Returns(new ProjectionCheckpointTracker.ProjectionCheckpointMigrationMarker(true));
        _ = daprClient.GetStateAsync<ProjectionCheckpoint>("statestore", legacyKey, cancellationToken: Arg.Any<CancellationToken>())
            .Returns(new ProjectionCheckpoint("test-tenant", "test-domain", "agg-001", 9, DateTimeOffset.UtcNow));
        ProjectionCheckpointTracker tracker = CreateTracker(daprClient);

        // Act
        long result = await tracker.ReadDeliveredSequenceAsync(TestIdentity, ProjectionName);

        // Assert — returns 0 and never consults the legacy key.
        result.ShouldBe(0);
        _ = await daprClient.DidNotReceive().GetStateAsync<ProjectionCheckpoint>(
            "statestore",
            legacyKey,
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TryEraseAsync_DeletesScopedKeyWithFirstWriteAndLeavesMarker() {
        // Arrange
        DaprClient daprClient = Substitute.For<DaprClient>();
        string scopedKey = ProjectionCheckpointTracker.GetProjectionScopedStateKey(TestIdentity, ProjectionName);
        string markerKey = ProjectionCheckpointTracker.GetMigratedMarkerKey(TestIdentity, ProjectionName);
        _ = daprClient.TryDeleteStateAsync(
                "statestore",
                scopedKey,
                "etag-x",
                stateOptions: Arg.Any<StateOptions?>(),
                metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(true);
        ProjectionCheckpointTracker tracker = CreateTracker(daprClient);

        // Act
        bool erased = await tracker.TryEraseAsync(TestIdentity, ProjectionName, "etag-x");

        // Assert — deletes the scoped key under FirstWrite and never touches the marker key.
        erased.ShouldBeTrue();
        _ = await daprClient.Received(1).TryDeleteStateAsync(
            "statestore",
            scopedKey,
            "etag-x",
            Arg.Is<StateOptions?>(value => value != null && value.Concurrency == ConcurrencyMode.FirstWrite),
            metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
            cancellationToken: Arg.Any<CancellationToken>());
        _ = await daprClient.DidNotReceive().TryDeleteStateAsync(
            "statestore",
            markerKey,
            Arg.Any<string>(),
            stateOptions: Arg.Any<StateOptions?>(),
            metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveDeliveredSequenceAsync_SavesToScopedKeyAndMarksMigrated() {
        // Arrange
        DaprClient daprClient = Substitute.For<DaprClient>();
        string scopedKey = ProjectionCheckpointTracker.GetProjectionScopedStateKey(TestIdentity, ProjectionName);
        string markerKey = ProjectionCheckpointTracker.GetMigratedMarkerKey(TestIdentity, ProjectionName);
        SetupGetStateAndEtag(daprClient, scopedKey, null, "etag-1");
        _ = daprClient.TrySaveStateAsync(
                "statestore",
                scopedKey,
                Arg.Any<ProjectionCheckpoint>(),
                "etag-1",
                stateOptions: Arg.Any<StateOptions?>(),
                metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(true);
        ProjectionCheckpointTracker tracker = CreateTracker(daprClient);

        // Act
        bool saved = await tracker.SaveDeliveredSequenceAsync(TestIdentity, ProjectionName, 9);

        // Assert — the checkpoint is written to the scoped key and the migration marker is finalized.
        saved.ShouldBeTrue();
        _ = await daprClient.Received(1).TrySaveStateAsync(
            "statestore",
            scopedKey,
            Arg.Is<ProjectionCheckpoint>(p =>
                p.AggregateId == "agg-001"
                && p.LastDeliveredSequence == 9),
            "etag-1",
            stateOptions: Arg.Any<StateOptions?>(),
            metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
            cancellationToken: Arg.Any<CancellationToken>());
        await daprClient.Received(1).SaveStateAsync(
            "statestore",
            markerKey,
            Arg.Any<ProjectionCheckpointTracker.ProjectionCheckpointMigrationMarker>(),
            stateOptions: Arg.Any<StateOptions?>(),
            metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReadDeliveredSequenceAsync_TwoProjectionsSameAggregate_HaveIndependentScopedCheckpoints() {
        // Arrange — two projections of the same aggregate resolve to distinct scoped keys with
        // independent high-water marks.
        DaprClient daprClient = Substitute.For<DaprClient>();
        string summaryKey = ProjectionCheckpointTracker.GetProjectionScopedStateKey(TestIdentity, "counter-summary");
        string detailKey = ProjectionCheckpointTracker.GetProjectionScopedStateKey(TestIdentity, "counter-detail");
        _ = daprClient.GetStateAsync<ProjectionCheckpoint>("statestore", summaryKey, cancellationToken: Arg.Any<CancellationToken>())
            .Returns(new ProjectionCheckpoint("test-tenant", "test-domain", "agg-001", 5, DateTimeOffset.UtcNow));
        _ = daprClient.GetStateAsync<ProjectionCheckpoint>("statestore", detailKey, cancellationToken: Arg.Any<CancellationToken>())
            .Returns(new ProjectionCheckpoint("test-tenant", "test-domain", "agg-001", 12, DateTimeOffset.UtcNow));
        ProjectionCheckpointTracker tracker = CreateTracker(daprClient);

        // Act
        long summary = await tracker.ReadDeliveredSequenceAsync(TestIdentity, "counter-summary");
        long detail = await tracker.ReadDeliveredSequenceAsync(TestIdentity, "counter-detail");

        // Assert
        summaryKey.ShouldNotBe(detailKey);
        summary.ShouldBe(5);
        detail.ShouldBe(12);
    }

    [Fact]
    public void ReleasedCheckpointTrackerInterface_PublicShapeUnchanged() {
        // AC 2: the released IProjectionCheckpointTracker contract must remain source/binary compatible.
        // Its public member set must be exactly the four pre-existing methods, and it must NOT expose
        // any of the projection-scoped members added to the internal IProjectionDeliveryCheckpointStore.
        string[] expected =
        [
            nameof(IProjectionCheckpointTracker.ReadLastDeliveredSequenceAsync),
            nameof(IProjectionCheckpointTracker.SaveDeliveredSequenceAsync),
            nameof(IProjectionCheckpointTracker.TrackIdentityAsync),
            nameof(IProjectionCheckpointTracker.EnumerateTrackedIdentitiesAsync),
        ];

        string[] actual = typeof(IProjectionCheckpointTracker)
            .GetMembers()
            .Select(member => member.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        actual.ShouldBe(expected.OrderBy(name => name, StringComparer.Ordinal).ToArray());
        actual.ShouldNotContain(nameof(IProjectionDeliveryCheckpointStore.ReadDeliveredSequenceAsync));
        actual.ShouldNotContain("TryEraseAsync");
    }

    private static ProjectionCheckpointTracker CreateTracker(
        DaprClient daprClient,
        string stateStoreName = "statestore",
        ILogger<ProjectionCheckpointTracker>? logger = null) =>
        new(daprClient, Options.Create(new ProjectionOptions { CheckpointStateStoreName = stateStoreName }), logger ?? NullLogger<ProjectionCheckpointTracker>.Instance);

    private static void SetupGetStateAndEtag(
        DaprClient daprClient,
        string key,
        ProjectionCheckpoint? value,
        string etag) => _ = daprClient.GetStateAndETagAsync<ProjectionCheckpoint>(
            "statestore",
            key,
            consistencyMode: Arg.Any<ConsistencyMode?>(),
            metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(((ProjectionCheckpoint, string))(value!, etag));
}
