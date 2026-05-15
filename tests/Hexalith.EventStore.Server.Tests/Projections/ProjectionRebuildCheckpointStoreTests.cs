using Dapr;
using Dapr.Client;

using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Projections;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Projections;

public class ProjectionRebuildCheckpointStoreTests {
    private static readonly ProjectionRebuildCheckpointScope Scope = new(
        "tenant-a",
        "party",
        "party-summary",
        "party-1",
        "operation-1");

    [Fact]
    public async Task SaveAsyncMissingCheckpointWritesInitialProgressWithEtag() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        SetupGetStateAndEtag(daprClient, null, string.Empty);
        SetupTrySave(daprClient, true);
        var store = CreateStore(daprClient);

        ProjectionRebuildCheckpointSaveResult result = await store.SaveAsync(
            Scope,
            lastAppliedSequence: 10,
            ProjectionRebuildStatus.Running);

        result.Succeeded.ShouldBeTrue();
        result.Checkpoint.ShouldNotBeNull();
        result.Checkpoint.LastAppliedSequence.ShouldBe(10);
        _ = await daprClient.Received(1).TrySaveStateAsync(
            "statestore",
            ProjectionRebuildCheckpointStore.GetStateKey(Scope),
            Arg.Is<ProjectionRebuildCheckpoint>(checkpoint =>
                checkpoint.Tenant == Scope.Tenant
                && checkpoint.Domain == Scope.Domain
                && checkpoint.ProjectionName == Scope.ProjectionName
                && checkpoint.LastAppliedSequence == 10
                && checkpoint.Status == ProjectionRebuildStatus.Running),
            string.Empty,
            stateOptions: Arg.Any<StateOptions?>(),
            metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveAsyncExistingHigherCheckpointDoesNotRegressOrWrite() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        ProjectionRebuildCheckpoint existing = CreateCheckpoint(25, ProjectionRebuildStatus.Running);
        SetupGetStateAndEtag(daprClient, existing, "etag-1");
        var store = CreateStore(daprClient);

        ProjectionRebuildCheckpointSaveResult result = await store.SaveAsync(
            Scope,
            lastAppliedSequence: 10,
            ProjectionRebuildStatus.Running);

        result.Succeeded.ShouldBeTrue();
        result.Checkpoint.ShouldBe(existing);
        _ = await daprClient.DidNotReceiveWithAnyArgs().TrySaveStateAsync<ProjectionRebuildCheckpoint>(
            default!,
            default!,
            default!,
            default!,
            stateOptions: default,
            metadata: default,
            cancellationToken: default);
    }

    [Fact]
    public async Task SaveAsyncDuplicatePageIsIdempotent() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        ProjectionRebuildCheckpoint existing = CreateCheckpoint(10, ProjectionRebuildStatus.Running);
        SetupGetStateAndEtag(daprClient, existing, "etag-1");
        var store = CreateStore(daprClient);

        ProjectionRebuildCheckpointSaveResult result = await store.SaveAsync(
            Scope,
            lastAppliedSequence: 10,
            ProjectionRebuildStatus.Running);

        result.Succeeded.ShouldBeTrue();
        result.Checkpoint.ShouldBe(existing);
        _ = await daprClient.DidNotReceiveWithAnyArgs().TrySaveStateAsync<ProjectionRebuildCheckpoint>(
            default!,
            default!,
            default!,
            default!,
            stateOptions: default,
            metadata: default,
            cancellationToken: default);
    }

    [Fact]
    public async Task SaveAsyncRetriesEtagConflictsAndReturnsCheckpointConflictWhenExhausted() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        SetupGetStateAndEtag(daprClient, null, "etag-1");
        SetupTrySave(daprClient, false);
        var store = CreateStore(daprClient);

        ProjectionRebuildCheckpointSaveResult result = await store.SaveAsync(
            Scope,
            lastAppliedSequence: 11,
            ProjectionRebuildStatus.Running);

        result.Succeeded.ShouldBeFalse();
        result.ReasonCode.ShouldBe(StreamReplayReasonCodes.CheckpointConflict);
        _ = await daprClient.Received(3).TrySaveStateAsync(
            "statestore",
            ProjectionRebuildCheckpointStore.GetStateKey(Scope),
            Arg.Any<ProjectionRebuildCheckpoint>(),
            Arg.Any<string>(),
            stateOptions: Arg.Any<StateOptions?>(),
            metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveAsyncStatusOnlyTransitionPersistsNewStatusWithoutLoweringSequence() {
        // P2 regression test: with an existing checkpoint at sequence=50 Status=Running,
        // a Pause transition passing lastAppliedSequence=existing must persist Paused.
        // Previously the early-return guard silently dropped the write because Running
        // is a progress status and 50 >= 50.
        DaprClient daprClient = Substitute.For<DaprClient>();
        ProjectionRebuildCheckpoint existing = CreateCheckpoint(50, ProjectionRebuildStatus.Running);
        SetupGetStateAndEtag(daprClient, existing, "etag-1");
        SetupTrySave(daprClient, true);
        var store = CreateStore(daprClient);

        ProjectionRebuildCheckpointSaveResult result = await store.SaveAsync(
            Scope,
            lastAppliedSequence: 50,
            ProjectionRebuildStatus.Paused);

        result.Succeeded.ShouldBeTrue();
        result.Checkpoint.ShouldNotBeNull();
        result.Checkpoint.Status.ShouldBe(ProjectionRebuildStatus.Paused);
        result.Checkpoint.LastAppliedSequence.ShouldBe(50);
        _ = await daprClient.Received(1).TrySaveStateAsync(
            "statestore",
            ProjectionRebuildCheckpointStore.GetStateKey(Scope),
            Arg.Is<ProjectionRebuildCheckpoint>(checkpoint =>
                checkpoint.Status == ProjectionRebuildStatus.Paused
                && checkpoint.LastAppliedSequence == 50),
            "etag-1",
            stateOptions: Arg.Any<StateOptions?>(),
            metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveAsyncStorageUnavailableReturnsStableReasonWithoutBlindSave() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.GetStateAndETagAsync<ProjectionRebuildCheckpoint>(
                "statestore",
                ProjectionRebuildCheckpointStore.GetStateKey(Scope),
                consistencyMode: Arg.Any<ConsistencyMode?>(),
                metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(Task.FromException<(ProjectionRebuildCheckpoint, string)>(new DaprException("state down")));
        var store = CreateStore(daprClient);

        ProjectionRebuildCheckpointSaveResult result = await store.SaveAsync(
            Scope,
            lastAppliedSequence: 11,
            ProjectionRebuildStatus.Failed,
            StreamReplayReasonCodes.ProjectionApplyRejected);

        result.Succeeded.ShouldBeFalse();
        result.ReasonCode.ShouldBe(StreamReplayReasonCodes.CheckpointUnavailable);
        _ = await daprClient.DidNotReceiveWithAnyArgs().TrySaveStateAsync<ProjectionRebuildCheckpoint>(
            default!,
            default!,
            default!,
            default!,
            stateOptions: default,
            metadata: default,
            cancellationToken: default);
    }

    [Fact]
    public async Task ResetAsyncCanRewindExistingCheckpointWithFreshOperationMetadata() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        ProjectionRebuildCheckpoint existing = CreateCheckpoint(50, ProjectionRebuildStatus.Running);
        SetupGetStateAndEtag(daprClient, existing, "etag-1");
        SetupTrySave(daprClient, true);
        var store = CreateStore(daprClient);
        var freshScope = Scope with { OperationId = "operation-2" };

        ProjectionRebuildCheckpointSaveResult result = await store.ResetAsync(
            freshScope,
            lastAppliedSequence: 0,
            ProjectionRebuildStatus.NotStarted);

        result.Succeeded.ShouldBeTrue();
        result.Checkpoint.ShouldNotBeNull();
        result.Checkpoint.LastAppliedSequence.ShouldBe(0);
        result.Checkpoint.OperationId.ShouldBe("operation-2");
        _ = await daprClient.Received(1).TrySaveStateAsync(
            "statestore",
            ProjectionRebuildCheckpointStore.GetStateKey(freshScope),
            Arg.Is<ProjectionRebuildCheckpoint>(checkpoint =>
                checkpoint.LastAppliedSequence == 0
                && checkpoint.OperationId == "operation-2"
                && checkpoint.Status == ProjectionRebuildStatus.NotStarted),
            "etag-1",
            stateOptions: Arg.Any<StateOptions?>(),
            metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveAsyncPropagatesProgrammerErrorsInsteadOfClassifyingUnavailable() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.GetStateAndETagAsync<ProjectionRebuildCheckpoint>(
                "statestore",
                ProjectionRebuildCheckpointStore.GetStateKey(Scope),
                consistencyMode: Arg.Any<ConsistencyMode?>(),
                metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(Task.FromException<(ProjectionRebuildCheckpoint, string)>(new InvalidOperationException("serializer defect")));
        var store = CreateStore(daprClient);

        await Should.ThrowAsync<InvalidOperationException>(() => store.SaveAsync(
            Scope,
            lastAppliedSequence: 11,
            ProjectionRebuildStatus.Failed,
            StreamReplayReasonCodes.ProjectionApplyRejected));
    }

    private static ProjectionRebuildCheckpointStore CreateStore(DaprClient daprClient)
        => new(
            daprClient,
            Options.Create(new ProjectionOptions { CheckpointStateStoreName = "statestore" }),
            NullLogger<ProjectionRebuildCheckpointStore>.Instance);

    private static ProjectionRebuildCheckpoint CreateCheckpoint(long lastAppliedSequence, ProjectionRebuildStatus status)
        => new(
            Scope.Tenant,
            Scope.Domain,
            Scope.ProjectionName,
            Scope.AggregateId,
            Scope.OperationId,
            lastAppliedSequence,
            status,
            DateTimeOffset.UtcNow,
            null);

    private static void SetupGetStateAndEtag(
        DaprClient daprClient,
        ProjectionRebuildCheckpoint? value,
        string etag)
        => _ = daprClient.GetStateAndETagAsync<ProjectionRebuildCheckpoint>(
                "statestore",
                ProjectionRebuildCheckpointStore.GetStateKey(Scope),
                consistencyMode: Arg.Any<ConsistencyMode?>(),
                metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(((ProjectionRebuildCheckpoint, string))(value!, etag));

    private static void SetupTrySave(DaprClient daprClient, bool result)
        => _ = daprClient.TrySaveStateAsync(
                "statestore",
                ProjectionRebuildCheckpointStore.GetStateKey(Scope),
                Arg.Any<ProjectionRebuildCheckpoint>(),
                Arg.Any<string>(),
                stateOptions: Arg.Any<StateOptions?>(),
                metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(result);
}
