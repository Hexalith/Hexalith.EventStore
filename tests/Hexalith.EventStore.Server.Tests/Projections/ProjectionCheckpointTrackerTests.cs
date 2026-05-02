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
        SetupGetStateAndEtag(daprClient, "custom-store", null, string.Empty);
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
            string.Empty,
            stateOptions: Arg.Any<StateOptions?>(),
            metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReadLastDeliveredSequenceAsync_MismatchedCheckpointIdentity_ThrowsInvalidOperationException() {
        // Arrange
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.GetStateAsync<ProjectionCheckpoint>(
                "statestore",
                ProjectionCheckpointTracker.GetStateKey(TestIdentity),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(new ProjectionCheckpoint("other-tenant", "test-domain", "agg-001", 42, DateTimeOffset.UtcNow));
        var tracker = CreateTracker(daprClient);

        // Act & Assert
        _ = await Should.ThrowAsync<InvalidOperationException>(() => tracker.ReadLastDeliveredSequenceAsync(TestIdentity));
    }

    [Fact]
    public async Task SaveDeliveredSequenceAsync_ExistingHigherCheckpoint_SkipsSaveAndReturnsTrue() {
        // Existing checkpoint already covers the proposed sequence, so the tracker must
        // short-circuit before issuing a TrySaveStateAsync. This avoids burning ETag
        // retries (and the misleading CheckpointSaveExhausted warning) under concurrent
        // fan-out where out-of-order triggers collide on a key that does not need to advance.
        DaprClient daprClient = Substitute.For<DaprClient>();
        SetupGetStateAndEtag(
            daprClient,
            "statestore",
            new ProjectionCheckpoint("test-tenant", "test-domain", "agg-001", 15, DateTimeOffset.UtcNow),
            "etag-2");
        var tracker = CreateTracker(daprClient);

        bool saved = await tracker.SaveDeliveredSequenceAsync(TestIdentity, 8);

        saved.ShouldBeTrue();
        _ = await daprClient.DidNotReceiveWithAnyArgs().TrySaveStateAsync<ProjectionCheckpoint>(
            default!,
            default!,
            default!,
            default!,
            stateOptions: default,
            metadata: default,
            cancellationToken: default);
    }

    [Fact]
    public async Task SaveDeliveredSequenceAsync_ExistingEqualCheckpoint_SkipsSaveAndReturnsTrue() {
        // Equal-sequence triggers (the duplicate-delivery shape) must also short-circuit
        // so duplicate fan-outs do not refresh UpdatedAt and do not collide on ETag.
        DaprClient daprClient = Substitute.For<DaprClient>();
        SetupGetStateAndEtag(
            daprClient,
            "statestore",
            new ProjectionCheckpoint("test-tenant", "test-domain", "agg-001", 12, DateTimeOffset.UtcNow),
            "etag-3");
        var tracker = CreateTracker(daprClient);

        bool saved = await tracker.SaveDeliveredSequenceAsync(TestIdentity, 12);

        saved.ShouldBeTrue();
        _ = await daprClient.DidNotReceiveWithAnyArgs().TrySaveStateAsync<ProjectionCheckpoint>(
            default!,
            default!,
            default!,
            default!,
            stateOptions: default,
            metadata: default,
            cancellationToken: default);
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
    public async Task SaveDeliveredSequenceAsync_TransientStorageException_RetriesUntilSaveSucceeds() {
        // Arrange
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.GetStateAndETagAsync<ProjectionCheckpoint>(
                "statestore",
                ProjectionCheckpointTracker.GetStateKey(TestIdentity),
                consistencyMode: Arg.Any<ConsistencyMode?>(),
                metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(
                Task.FromException<(ProjectionCheckpoint, string)>(new InvalidOperationException("transient state store failure")),
                Task.FromResult(((ProjectionCheckpoint, string))(null!, string.Empty)));
        SetupTrySave(daprClient, "statestore", true);
        var tracker = CreateTracker(daprClient);

        // Act
        bool saved = await tracker.SaveDeliveredSequenceAsync(TestIdentity, 7);

        // Assert
        saved.ShouldBeTrue();
        _ = await daprClient.Received(1).TrySaveStateAsync(
            "statestore",
            ProjectionCheckpointTracker.GetStateKey(TestIdentity),
            Arg.Any<ProjectionCheckpoint>(),
            string.Empty,
            stateOptions: Arg.Any<StateOptions?>(),
            metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveDeliveredSequenceAsync_MismatchedCheckpointIdentity_ThrowsInvalidOperationException() {
        // Arrange -- mirror the read-path identity guard so the save path cannot silently
        // accept a stored record whose identity does not match the requested aggregate.
        DaprClient daprClient = Substitute.For<DaprClient>();
        SetupGetStateAndEtag(
            daprClient,
            "statestore",
            new ProjectionCheckpoint("other-tenant", "test-domain", "agg-001", 5, DateTimeOffset.UtcNow),
            "etag-1");
        var tracker = CreateTracker(daprClient);

        // Act & Assert
        _ = await Should.ThrowAsync<InvalidOperationException>(() => tracker.SaveDeliveredSequenceAsync(TestIdentity, 7));
        _ = await daprClient.DidNotReceiveWithAnyArgs().TrySaveStateAsync<ProjectionCheckpoint>(
            default!,
            default!,
            default!,
            default!,
            stateOptions: default,
            metadata: default,
            cancellationToken: default);
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

    [Fact]
    public async Task TrackIdentityAsync_RetryAfterPriorFailure_CompletesRegistrationOnSecondCall() {
        // AC #3 / Round-2 D2 closure: "Registration failure is logged and swallowed... a later
        // event for the same identity may retry registration." Pin that a previously-failed
        // registration leaves the tracker with no orphan in-memory state — a subsequent call
        // with the same identity completes the registration end-to-end.
        DaprClient daprClient = Substitute.For<DaprClient>();
        var tracker = CreateTracker(daprClient);

        bool failScopeIndexRead = true;
        _ = daprClient.GetStateAndETagAsync<ProjectionCheckpointTracker.ProjectionIdentityIndex>(
                "statestore",
                Arg.Any<string>(),
                consistencyMode: Arg.Any<ConsistencyMode?>(),
                metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => failScopeIndexRead
                ? throw new InvalidOperationException("transient storage outage")
                : (((ProjectionCheckpointTracker.ProjectionIdentityIndex?)null)!, string.Empty));
        _ = daprClient.GetStateAndETagAsync<ProjectionCheckpointTracker.ProjectionIdentityScopePage>(
                "statestore",
                Arg.Any<string>(),
                consistencyMode: Arg.Any<ConsistencyMode?>(),
                metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns((((ProjectionCheckpointTracker.ProjectionIdentityScopePage?)null)!, string.Empty));
        _ = daprClient.GetStateAndETagAsync<ProjectionCheckpointTracker.ProjectionIdentityPage>(
                "statestore",
                Arg.Any<string>(),
                consistencyMode: Arg.Any<ConsistencyMode?>(),
                metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns((((ProjectionCheckpointTracker.ProjectionIdentityPage?)null)!, string.Empty));
        _ = daprClient.TrySaveStateAsync(
                "statestore",
                Arg.Any<string>(),
                Arg.Any<ProjectionCheckpointTracker.ProjectionIdentityScopePage>(),
                Arg.Any<string>(),
                stateOptions: Arg.Any<StateOptions?>(),
                metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(true);
        _ = daprClient.TrySaveStateAsync(
                "statestore",
                Arg.Any<string>(),
                Arg.Any<ProjectionCheckpointTracker.ProjectionIdentityPage>(),
                Arg.Any<string>(),
                stateOptions: Arg.Any<StateOptions?>(),
                metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(true);
        _ = daprClient.TrySaveStateAsync(
                "statestore",
                Arg.Any<string>(),
                Arg.Any<ProjectionCheckpointTracker.ProjectionIdentityIndex>(),
                Arg.Any<string>(),
                stateOptions: Arg.Any<StateOptions?>(),
                metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(true);

        // Act -- first call: the storage outage propagates through the bounded ETag retry loop.
        _ = await Should.ThrowAsync<InvalidOperationException>(() => tracker.TrackIdentityAsync(TestIdentity));

        // Recover the storage backend.
        failScopeIndexRead = false;

        // Act -- second call with the same identity must succeed cleanly without orphan state.
        await tracker.TrackIdentityAsync(TestIdentity);

        // Assert -- registration end-to-end persisted scope page, scope index, identity page,
        // and identity index. If the tracker had cached the prior failure (e.g. via an in-memory
        // "already attempted" flag), no save would happen on the retry.
        _ = await daprClient.Received().TrySaveStateAsync(
            "statestore",
            Arg.Any<string>(),
            Arg.Is<ProjectionCheckpointTracker.ProjectionIdentityScopePage>(p => (p.Scopes ?? Array.Empty<ProjectionCheckpointTracker.ProjectionIdentityScope>()).Any(s => s.TenantId == "test-tenant" && s.Domain == "test-domain")),
            Arg.Any<string>(),
            stateOptions: Arg.Any<StateOptions?>(),
            metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
            cancellationToken: Arg.Any<CancellationToken>());
        _ = await daprClient.Received().TrySaveStateAsync(
            "statestore",
            Arg.Any<string>(),
            Arg.Is<ProjectionCheckpointTracker.ProjectionIdentityPage>(p => (p.Identities ?? Array.Empty<ProjectionCheckpointTracker.ProjectionIdentity>()).Any(i => i.AggregateId == "agg-001")),
            Arg.Any<string>(),
            stateOptions: Arg.Any<StateOptions?>(),
            metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TrackIdentityAsync_IndexRetryExhausted_ThrowsSoPublisherCanLogFailure() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        var tracker = CreateTracker(daprClient);

        _ = daprClient.GetStateAndETagAsync<ProjectionCheckpointTracker.ProjectionIdentityIndex>(
                "statestore",
                Arg.Any<string>(),
                consistencyMode: Arg.Any<ConsistencyMode?>(),
                metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns((((ProjectionCheckpointTracker.ProjectionIdentityIndex?)null)!, string.Empty));
        _ = daprClient.GetStateAndETagAsync<ProjectionCheckpointTracker.ProjectionIdentityScopePage>(
                "statestore",
                Arg.Any<string>(),
                consistencyMode: Arg.Any<ConsistencyMode?>(),
                metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns((((ProjectionCheckpointTracker.ProjectionIdentityScopePage?)null)!, string.Empty));
        _ = daprClient.TrySaveStateAsync(
                "statestore",
                Arg.Any<string>(),
                Arg.Any<ProjectionCheckpointTracker.ProjectionIdentityScopePage>(),
                Arg.Any<string>(),
                stateOptions: Arg.Any<StateOptions?>(),
                metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(true);
        _ = daprClient.TrySaveStateAsync(
                "statestore",
                Arg.Any<string>(),
                Arg.Any<ProjectionCheckpointTracker.ProjectionIdentityIndex>(),
                Arg.Any<string>(),
                stateOptions: Arg.Any<StateOptions?>(),
                metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(false);

        _ = await Should.ThrowAsync<InvalidOperationException>(() => tracker.TrackIdentityAsync(TestIdentity));
    }

    [Fact]
    public async Task TrackIdentityAsync_ExistingScopeScan_UsesEtagGuardedPageRead() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        var tracker = CreateTracker(daprClient);
        var existingScope = new ProjectionCheckpointTracker.ProjectionIdentityScope("test-tenant", "test-domain");

        _ = daprClient.GetStateAndETagAsync<ProjectionCheckpointTracker.ProjectionIdentityIndex>(
                "statestore",
                "projection-identities:scopes",
                consistencyMode: Arg.Any<ConsistencyMode?>(),
                metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns((new ProjectionCheckpointTracker.ProjectionIdentityIndex(1, 1), "scope-index-etag"));
        _ = daprClient.GetStateAndETagAsync<ProjectionCheckpointTracker.ProjectionIdentityScopePage>(
                "statestore",
                "projection-identities:scopes:0",
                consistencyMode: Arg.Any<ConsistencyMode?>(),
                metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns((new ProjectionCheckpointTracker.ProjectionIdentityScopePage([existingScope]), "scope-page-etag"));
        _ = daprClient.GetStateAndETagAsync<ProjectionCheckpointTracker.ProjectionIdentityIndex>(
                "statestore",
                "projection-identities:index:test-tenant:test-domain",
                consistencyMode: Arg.Any<ConsistencyMode?>(),
                metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns((((ProjectionCheckpointTracker.ProjectionIdentityIndex?)null)!, string.Empty));
        _ = daprClient.GetStateAndETagAsync<ProjectionCheckpointTracker.ProjectionIdentityPage>(
                "statestore",
                Arg.Any<string>(),
                consistencyMode: Arg.Any<ConsistencyMode?>(),
                metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns((((ProjectionCheckpointTracker.ProjectionIdentityPage?)null)!, string.Empty));
        _ = daprClient.TrySaveStateAsync(
                "statestore",
                Arg.Any<string>(),
                Arg.Any<ProjectionCheckpointTracker.ProjectionIdentityPage>(),
                Arg.Any<string>(),
                stateOptions: Arg.Any<StateOptions?>(),
                metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(true);
        _ = daprClient.TrySaveStateAsync(
                "statestore",
                Arg.Any<string>(),
                Arg.Any<ProjectionCheckpointTracker.ProjectionIdentityIndex>(),
                Arg.Any<string>(),
                stateOptions: Arg.Any<StateOptions?>(),
                metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(true);

        await tracker.TrackIdentityAsync(TestIdentity);

        _ = await daprClient.Received(1).GetStateAndETagAsync<ProjectionCheckpointTracker.ProjectionIdentityScopePage>(
            "statestore",
            "projection-identities:scopes:0",
            consistencyMode: Arg.Any<ConsistencyMode?>(),
            metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
            cancellationToken: Arg.Any<CancellationToken>());
        _ = await daprClient.DidNotReceive().GetStateAsync<ProjectionCheckpointTracker.ProjectionIdentityScopePage>(
            "statestore",
            "projection-identities:scopes:0",
            consistencyMode: Arg.Any<ConsistencyMode?>(),
            metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TrackIdentityAsync_MixedCaseInput_PersistsCanonicalLowercaseScopeAndIdentity() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        var tracker = CreateTracker(daprClient);
        var mixedCaseIdentity = new AggregateIdentity("Test-Tenant", "Test-Domain", "Agg-001");

        _ = daprClient.GetStateAndETagAsync<ProjectionCheckpointTracker.ProjectionIdentityIndex>(
                "statestore",
                Arg.Any<string>(),
                consistencyMode: Arg.Any<ConsistencyMode?>(),
                metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns((((ProjectionCheckpointTracker.ProjectionIdentityIndex?)null)!, string.Empty));
        _ = daprClient.GetStateAndETagAsync<ProjectionCheckpointTracker.ProjectionIdentityScopePage>(
                "statestore",
                Arg.Any<string>(),
                consistencyMode: Arg.Any<ConsistencyMode?>(),
                metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns((((ProjectionCheckpointTracker.ProjectionIdentityScopePage?)null)!, string.Empty));
        _ = daprClient.GetStateAndETagAsync<ProjectionCheckpointTracker.ProjectionIdentityPage>(
                "statestore",
                Arg.Any<string>(),
                consistencyMode: Arg.Any<ConsistencyMode?>(),
                metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns((((ProjectionCheckpointTracker.ProjectionIdentityPage?)null)!, string.Empty));
        _ = daprClient.TrySaveStateAsync(
                "statestore",
                Arg.Any<string>(),
                Arg.Any<ProjectionCheckpointTracker.ProjectionIdentityScopePage>(),
                Arg.Any<string>(),
                stateOptions: Arg.Any<StateOptions?>(),
                metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(true);
        _ = daprClient.TrySaveStateAsync(
                "statestore",
                Arg.Any<string>(),
                Arg.Any<ProjectionCheckpointTracker.ProjectionIdentityPage>(),
                Arg.Any<string>(),
                stateOptions: Arg.Any<StateOptions?>(),
                metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(true);
        _ = daprClient.TrySaveStateAsync(
                "statestore",
                Arg.Any<string>(),
                Arg.Any<ProjectionCheckpointTracker.ProjectionIdentityIndex>(),
                Arg.Any<string>(),
                stateOptions: Arg.Any<StateOptions?>(),
                metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(true);

        await tracker.TrackIdentityAsync(mixedCaseIdentity);

        _ = await daprClient.Received().TrySaveStateAsync(
            "statestore",
            Arg.Any<string>(),
            Arg.Is<ProjectionCheckpointTracker.ProjectionIdentityScopePage>(p => (p.Scopes ?? Array.Empty<ProjectionCheckpointTracker.ProjectionIdentityScope>()).Any(s => s.TenantId == "test-tenant" && s.Domain == "test-domain")),
            Arg.Any<string>(),
            stateOptions: Arg.Any<StateOptions?>(),
            metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
            cancellationToken: Arg.Any<CancellationToken>());
        _ = await daprClient.Received().TrySaveStateAsync(
            "statestore",
            Arg.Any<string>(),
            Arg.Is<ProjectionCheckpointTracker.ProjectionIdentityPage>(p => (p.Identities ?? Array.Empty<ProjectionCheckpointTracker.ProjectionIdentity>()).Any(i => i.TenantId == "test-tenant" && i.Domain == "test-domain" && i.AggregateId == "Agg-001")),
            Arg.Any<string>(),
            stateOptions: Arg.Any<StateOptions?>(),
            metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TrackIdentityAsync_FullIdentityPage_AppendsToNextPageAndUpdatesIndex() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        var tracker = CreateTracker(daprClient);
        var newIdentity = new AggregateIdentity("test-tenant", "test-domain", "agg-100");
        ProjectionCheckpointTracker.ProjectionIdentity[] fullPage = Enumerable
            .Range(0, 100)
            .Select(i => new ProjectionCheckpointTracker.ProjectionIdentity("test-tenant", "test-domain", $"agg-{i:D3}"))
            .ToArray();

        _ = daprClient.GetStateAndETagAsync<ProjectionCheckpointTracker.ProjectionIdentityIndex>(
                "statestore",
                "projection-identities:scopes",
                consistencyMode: Arg.Any<ConsistencyMode?>(),
                metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns((new ProjectionCheckpointTracker.ProjectionIdentityIndex(1, 1), "scope-index-etag"));
        _ = daprClient.GetStateAndETagAsync<ProjectionCheckpointTracker.ProjectionIdentityScopePage>(
                "statestore",
                "projection-identities:scopes:0",
                consistencyMode: Arg.Any<ConsistencyMode?>(),
                metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns((new ProjectionCheckpointTracker.ProjectionIdentityScopePage([new("test-tenant", "test-domain")]), "scope-page-etag"));
        _ = daprClient.GetStateAndETagAsync<ProjectionCheckpointTracker.ProjectionIdentityIndex>(
                "statestore",
                "projection-identities:index:test-tenant:test-domain",
                consistencyMode: Arg.Any<ConsistencyMode?>(),
                metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns((new ProjectionCheckpointTracker.ProjectionIdentityIndex(1, 100), "identity-index-etag"));
        _ = daprClient.GetStateAndETagAsync<ProjectionCheckpointTracker.ProjectionIdentityPage>(
                "statestore",
                "projection-identities:page:test-tenant:test-domain:0",
                consistencyMode: Arg.Any<ConsistencyMode?>(),
                metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns((new ProjectionCheckpointTracker.ProjectionIdentityPage(fullPage), "identity-page-0-etag"));
        _ = daprClient.GetStateAndETagAsync<ProjectionCheckpointTracker.ProjectionIdentityPage>(
                "statestore",
                "projection-identities:page:test-tenant:test-domain:1",
                consistencyMode: Arg.Any<ConsistencyMode?>(),
                metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns((((ProjectionCheckpointTracker.ProjectionIdentityPage?)null)!, string.Empty));
        _ = daprClient.TrySaveStateAsync(
                "statestore",
                Arg.Any<string>(),
                Arg.Any<ProjectionCheckpointTracker.ProjectionIdentityPage>(),
                Arg.Any<string>(),
                stateOptions: Arg.Any<StateOptions?>(),
                metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(true);
        _ = daprClient.TrySaveStateAsync(
                "statestore",
                Arg.Any<string>(),
                Arg.Any<ProjectionCheckpointTracker.ProjectionIdentityIndex>(),
                Arg.Any<string>(),
                stateOptions: Arg.Any<StateOptions?>(),
                metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(true);

        await tracker.TrackIdentityAsync(newIdentity);

        _ = await daprClient.Received(1).TrySaveStateAsync(
            "statestore",
            "projection-identities:page:test-tenant:test-domain:1",
            Arg.Is<ProjectionCheckpointTracker.ProjectionIdentityPage>(p => (p.Identities ?? Array.Empty<ProjectionCheckpointTracker.ProjectionIdentity>()).Length == 1 && (p.Identities ?? Array.Empty<ProjectionCheckpointTracker.ProjectionIdentity>())[0].AggregateId == "agg-100"),
            string.Empty,
            stateOptions: Arg.Any<StateOptions?>(),
            metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
            cancellationToken: Arg.Any<CancellationToken>());
        _ = await daprClient.Received(1).TrySaveStateAsync(
            "statestore",
            "projection-identities:index:test-tenant:test-domain",
            Arg.Is<ProjectionCheckpointTracker.ProjectionIdentityIndex>(i => i.PageCount == 2 && i.LastPageCount == 1),
            "identity-index-etag",
            stateOptions: Arg.Any<StateOptions?>(),
            metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
            cancellationToken: Arg.Any<CancellationToken>());
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
