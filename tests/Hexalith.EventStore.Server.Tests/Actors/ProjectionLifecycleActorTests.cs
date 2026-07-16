using System.Text.Json;

using Dapr.Actors;
using Dapr.Actors.Runtime;

using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Tests.TestUtilities;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

using NSubstitute;

using Shouldly;

using InMemoryStateManager = Hexalith.EventStore.Testing.Fakes.InMemoryStateManager;

namespace Hexalith.EventStore.Server.Tests.Actors;

/// <summary>
/// Tests for <see cref="ProjectionLifecycleActor"/> covering the erase/deliver admission
/// state machine and its per-turn persistence.
/// </summary>
public class ProjectionLifecycleActorTests {
    [Fact]
    public async Task BeginEraseAsync_IdleAndFreshBeginNotAllowed_RefusesWithoutLeavingIdle() {
        (ProjectionLifecycleActor actor, _) = CreateActor();

        ProjectionEraseAdmission admission = await actor.BeginEraseAsync(
            new ProjectionEraseBeginRequest("op-1", "digest-1", allowBegin: false));
        ProjectionDeliveryAdmission delivery = await actor.TryAdmitDeliveryWriteAsync();

        admission.Kind.ShouldBe(ProjectionEraseAdmissionKind.BeginNotAllowed);
        delivery.Admitted.ShouldBeTrue();
        delivery.Phase.ShouldBe(ProjectionLifecyclePhase.Idle);
    }

    private const string StateKey = "projection-lifecycle";

    private static (ProjectionLifecycleActor Actor, InMemoryStateManager StateManager) CreateActor(
        TimeProvider? timeProvider = null) {
        var stateManager = new InMemoryStateManager();
        ILogger<ProjectionLifecycleActor> logger = Substitute.For<ILogger<ProjectionLifecycleActor>>();
        var host = ActorHost.CreateForTest<ProjectionLifecycleActor>(
            new ActorTestOptions { ActorId = new ActorId("tenant:domain:agg:counter") });
        var actor = new ProjectionLifecycleActor(host, logger, timeProvider);

        ActorStateManagerTestHelper.SetStateManager(actor, stateManager);

        return (actor, stateManager);
    }

    private static ProjectionLifecycleActorState PersistedState(InMemoryStateManager stateManager) {
        stateManager.CommittedState.ShouldContainKey(StateKey);
        return (ProjectionLifecycleActorState)stateManager.CommittedState[StateKey];
    }

    [Fact]
    public async Task BeginAndCompleteRebuildAsync_PersistsTruthfulLifecycleAndDefersDelivery() {
        (ProjectionLifecycleActor actor, InMemoryStateManager stateManager) = CreateActor();

        bool begun = await actor.BeginRebuildAsync(new ProjectionRebuildLifecycleRequest("rebuild-1"));
        ProjectionDeliveryAdmission during = await actor.TryAdmitDeliveryWriteAsync();

        begun.ShouldBeTrue();
        during.Admitted.ShouldBeFalse();
        during.Phase.ShouldBe(ProjectionLifecyclePhase.Rebuilding);
        ProjectionLifecycleActorState rebuilding = PersistedState(stateManager);
        rebuilding.OperationId.ShouldBe("rebuild-1");
        (await actor.BeginRebuildAsync(new ProjectionRebuildLifecycleRequest("rebuild-1"))).ShouldBeTrue();
        (await actor.BeginRebuildAsync(new ProjectionRebuildLifecycleRequest("rebuild-2"))).ShouldBeFalse();

        (await actor.CompleteRebuildAsync(new ProjectionRebuildLifecycleRequest("rebuild-1"))).ShouldBeTrue();
        (await actor.ReadPhaseAsync()).ShouldBe(ProjectionLifecyclePhase.Idle);
        (await actor.TryAdmitDeliveryWriteAsync()).Admitted.ShouldBeTrue();
    }

    [Fact]
    public async Task BeginEraseAsync_WhileRebuilding_ConflictsWithoutReplacingLifecycle() {
        (ProjectionLifecycleActor actor, _) = CreateActor();
        _ = await actor.BeginRebuildAsync(new ProjectionRebuildLifecycleRequest("rebuild-1"));

        ProjectionEraseAdmission admission = await actor.BeginEraseAsync(
            new ProjectionEraseBeginRequest("erase-1", "digest-1"));

        admission.Kind.ShouldBe(ProjectionEraseAdmissionKind.Conflict);
        (await actor.ReadPhaseAsync()).ShouldBe(ProjectionLifecyclePhase.Rebuilding);
    }

    [Fact]
    public async Task DeliveryLease_BlocksRebuildAndEraseUntilMatchingCompletion() {
        (ProjectionLifecycleActor actor, _) = CreateActor();

        (await actor.BeginDeliveryWriteAsync(new ProjectionDeliveryLifecycleRequest("delivery-1"))).ShouldBeTrue();
        (await actor.BeginDeliveryWriteAsync(new ProjectionDeliveryLifecycleRequest("delivery-1"))).ShouldBeTrue();
        (await actor.BeginDeliveryWriteAsync(new ProjectionDeliveryLifecycleRequest("delivery-2"))).ShouldBeFalse();
        (await actor.BeginRebuildAsync(new ProjectionRebuildLifecycleRequest("rebuild-1"))).ShouldBeFalse();
        ProjectionEraseAdmission erase = await actor.BeginEraseAsync(
            new ProjectionEraseBeginRequest("erase-1", "digest-1"));

        erase.Kind.ShouldBe(ProjectionEraseAdmissionKind.Conflict);
        (await actor.CompleteDeliveryWriteAsync(new ProjectionDeliveryLifecycleRequest("delivery-2"))).ShouldBeFalse();
        (await actor.CompleteDeliveryWriteAsync(new ProjectionDeliveryLifecycleRequest("delivery-1"))).ShouldBeTrue();
        (await actor.BeginRebuildAsync(new ProjectionRebuildLifecycleRequest("rebuild-1"))).ShouldBeTrue();
    }

    [Fact]
    public async Task DeliveryLease_ExpiredCrashLeaseCanBeReclaimedByAnotherDurableDelivery() {
        DateTimeOffset now = new(2026, 7, 16, 8, 0, 0, TimeSpan.Zero);
        var clock = new FakeTimeProvider(now);
        (ProjectionLifecycleActor actor, _) = CreateActor(clock);
        (await actor.BeginDeliveryWriteAsync(
            new ProjectionDeliveryLifecycleRequest("delivery-1", now + TimeSpan.FromMinutes(1)))).ShouldBeTrue();

        clock.Advance(TimeSpan.FromMinutes(2));

        (await actor.BeginDeliveryWriteAsync(
            new ProjectionDeliveryLifecycleRequest("delivery-2", clock.GetUtcNow() + TimeSpan.FromMinutes(1)))).ShouldBeTrue();
        (await actor.CompleteDeliveryWriteAsync(new ProjectionDeliveryLifecycleRequest("delivery-1"))).ShouldBeFalse();
        (await actor.CompleteDeliveryWriteAsync(new ProjectionDeliveryLifecycleRequest("delivery-2"))).ShouldBeTrue();
    }

    [Fact]
    public async Task DeliveryLease_PreLeaseStateMigratesToBoundedExpiryBeforeReclamation() {
        DateTimeOffset now = new(2026, 7, 16, 8, 0, 0, TimeSpan.Zero);
        var clock = new FakeTimeProvider(now);
        (ProjectionLifecycleActor actor, InMemoryStateManager stateManager) = CreateActor(clock);
        await stateManager.SetStateAsync(
            StateKey,
            new ProjectionLifecycleActorState(
                ProjectionLifecyclePhase.Delivering,
                "legacy-delivery",
                ManifestDigest: null,
                new Dictionary<string, string>(StringComparer.Ordinal),
                Revision: 7));
        await stateManager.SaveStateAsync();

        (await actor.BeginDeliveryWriteAsync(
            new ProjectionDeliveryLifecycleRequest("delivery-2", now + TimeSpan.FromDays(1)))).ShouldBeFalse();
        ProjectionLifecycleActorState migrated = PersistedState(stateManager);
        migrated.Revision.ShouldBe(7);
        migrated.DeliveryLeaseExpiresAtUtc.ShouldBe(now + TimeSpan.FromMinutes(5));

        clock.Advance(TimeSpan.FromMinutes(6));

        (await actor.BeginDeliveryWriteAsync(
            new ProjectionDeliveryLifecycleRequest("delivery-2", clock.GetUtcNow() + TimeSpan.FromDays(1)))).ShouldBeTrue();
        ProjectionLifecycleActorState reclaimed = PersistedState(stateManager);
        reclaimed.OperationId.ShouldBe("delivery-2");
        reclaimed.Revision.ShouldBe(8);
        reclaimed.DeliveryLeaseExpiresAtUtc.ShouldBe(clock.GetUtcNow() + TimeSpan.FromMinutes(5));
    }

    [Fact]
    public async Task RebuildPromotionFence_RejectsTerminalCleanupUntilPromotionCompletes() {
        (ProjectionLifecycleActor actor, _) = CreateActor();
        var request = new ProjectionRebuildLifecycleRequest("rebuild-1");
        (await actor.BeginRebuildAsync(request)).ShouldBeTrue();
        (await actor.BeginRebuildPromotionAsync(request)).ShouldBeTrue();

        (await actor.CompleteRebuildAsync(request)).ShouldBeFalse();
        (await actor.CompleteRebuildPromotionAsync(request)).ShouldBeTrue();
        (await actor.CompleteRebuildAsync(request)).ShouldBeTrue();
    }

    [Fact]
    public async Task ReadSnapshotAsync_RevisionChangesOnlyOnPersistedLifecycleTransitions() {
        (ProjectionLifecycleActor actor, _) = CreateActor();

        (await actor.ReadSnapshotAsync()).ShouldBe(new ProjectionLifecycleSnapshot(ProjectionLifecyclePhase.Idle, 0));
        (await actor.BeginDeliveryWriteAsync(new ProjectionDeliveryLifecycleRequest("delivery-1"))).ShouldBeTrue();
        (await actor.ReadSnapshotAsync()).ShouldBe(new ProjectionLifecycleSnapshot(ProjectionLifecyclePhase.Delivering, 1));
        (await actor.BeginDeliveryWriteAsync(new ProjectionDeliveryLifecycleRequest("delivery-1"))).ShouldBeTrue();
        (await actor.ReadSnapshotAsync()).Revision.ShouldBe(1);
        (await actor.CompleteDeliveryWriteAsync(new ProjectionDeliveryLifecycleRequest("delivery-1"))).ShouldBeTrue();

        // Completing a normal delivery clears the lease and restores the absent baseline, so the
        // synthesized idle snapshot reads revision 0 rather than a persisted post-delivery record.
        (await actor.ReadSnapshotAsync()).ShouldBe(new ProjectionLifecycleSnapshot(ProjectionLifecyclePhase.Idle, 0));
    }

    [Fact]
    public async Task CompleteDeliveryWriteAsync_RestoresAbsentLifecycleBaseline() {
        (ProjectionLifecycleActor actor, InMemoryStateManager stateManager) = CreateActor();

        (await actor.BeginDeliveryWriteAsync(new ProjectionDeliveryLifecycleRequest("delivery-1"))).ShouldBeTrue();
        stateManager.CommittedState.ShouldContainKey(StateKey);

        (await actor.CompleteDeliveryWriteAsync(new ProjectionDeliveryLifecycleRequest("delivery-1"))).ShouldBeTrue();

        // A completed normal delivery must not leave a persisted lifecycle record: absent means idle.
        stateManager.CommittedState.ShouldNotContainKey(StateKey);
        (await actor.ReadPhaseAsync()).ShouldBe(ProjectionLifecyclePhase.Idle);
        (await actor.TryAdmitDeliveryWriteAsync()).Admitted.ShouldBeTrue();

        // The cleared baseline still serializes a following rebuild/erase turn correctly.
        (await actor.BeginRebuildAsync(new ProjectionRebuildLifecycleRequest("rebuild-1"))).ShouldBeTrue();
    }

    [Fact]
    public async Task BeginEraseAsync_FromIdle_AdmitsAndPersistsErasingPhase() {
        (ProjectionLifecycleActor actor, InMemoryStateManager stateManager) = CreateActor();

        ProjectionEraseAdmission admission = await actor.BeginEraseAsync(new ProjectionEraseBeginRequest("op-1", "digest-1"));

        admission.Kind.ShouldBe(ProjectionEraseAdmissionKind.Admitted);
        admission.PerTargetOutcomes.ShouldBeEmpty();

        ProjectionLifecycleActorState persisted = PersistedState(stateManager);
        persisted.Phase.ShouldBe(ProjectionLifecyclePhase.Erasing);
        persisted.OperationId.ShouldBe("op-1");
        persisted.ManifestDigest.ShouldBe("digest-1");

        ProjectionDeliveryAdmission delivery = await actor.TryAdmitDeliveryWriteAsync();
        delivery.Admitted.ShouldBeFalse();
        delivery.Phase.ShouldBe(ProjectionLifecyclePhase.Erasing);
    }

    [Fact]
    public async Task BeginEraseAsync_SameOperationWhileErasing_ResumesWithPriorOutcomes() {
        (ProjectionLifecycleActor actor, _) = CreateActor();
        _ = await actor.BeginEraseAsync(new ProjectionEraseBeginRequest("op-1", "digest-1"));
        (await actor.RecordTargetOutcomeAsync(new ProjectionTargetOutcomeRequest("op-1", "target-a", "erased"))).ShouldBeTrue();

        ProjectionEraseAdmission admission = await actor.BeginEraseAsync(new ProjectionEraseBeginRequest("op-1", "digest-1"));

        admission.Kind.ShouldBe(ProjectionEraseAdmissionKind.Resume);
        admission.PerTargetOutcomes.ShouldContainKeyAndValue("target-a", "erased");
    }

    [Fact]
    public async Task BeginEraseAsync_SameOperationWithFreshBeginDisallowed_StillResumes() {
        (ProjectionLifecycleActor actor, _) = CreateActor();
        _ = await actor.BeginEraseAsync(new ProjectionEraseBeginRequest("op-1", "digest-1"));
        (await actor.RecordTargetOutcomeAsync(new ProjectionTargetOutcomeRequest("op-1", "target-a", "erased"))).ShouldBeTrue();

        ProjectionEraseAdmission admission = await actor.BeginEraseAsync(
            new ProjectionEraseBeginRequest("op-1", "digest-1", allowBegin: false));

        admission.Kind.ShouldBe(ProjectionEraseAdmissionKind.Resume);
        admission.PerTargetOutcomes.ShouldContainKeyAndValue("target-a", "erased");
    }

    [Fact]
    public void ProjectionEraseBeginRequest_ReleasedConstructorAndDeconstructRemainAvailable() {
        Type requestType = typeof(ProjectionEraseBeginRequest);

        requestType.GetConstructor([typeof(string), typeof(string)]).ShouldNotBeNull();
        requestType.GetMethod("Deconstruct", [
            typeof(string).MakeByRefType(),
            typeof(string).MakeByRefType(),
        ]).ShouldNotBeNull();
    }

    [Fact]
    public void ProjectionEraseBeginRequest_ReleasedJsonPayload_DefaultsAllowBeginToTrue() {
        ProjectionEraseBeginRequest? request = JsonSerializer.Deserialize<ProjectionEraseBeginRequest>(
            """{"operationId":"op-1","manifestDigest":"digest-1"}""",
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        request.ShouldNotBeNull().AllowBegin.ShouldBeTrue();
    }

    [Fact]
    public async Task BeginEraseAsync_SameOperationDifferentManifest_ConflictsWithoutResume() {
        (ProjectionLifecycleActor actor, InMemoryStateManager stateManager) = CreateActor();
        _ = await actor.BeginEraseAsync(new ProjectionEraseBeginRequest("op-1", "digest-1"));
        (await actor.RecordTargetOutcomeAsync(new ProjectionTargetOutcomeRequest("op-1", "target-a", "erased"))).ShouldBeTrue();

        // Same operationId but a DIFFERENT target manifest must be refused, not resumed against a manifest
        // that was never admitted.
        ProjectionEraseAdmission admission = await actor.BeginEraseAsync(new ProjectionEraseBeginRequest("op-1", "digest-2"));

        admission.Kind.ShouldBe(ProjectionEraseAdmissionKind.Conflict);
        admission.PerTargetOutcomes.ShouldBeEmpty();

        // The in-flight operation and its recorded progress are untouched.
        ProjectionLifecycleActorState persisted = PersistedState(stateManager);
        persisted.OperationId.ShouldBe("op-1");
        persisted.ManifestDigest.ShouldBe("digest-1");
        persisted.PerTargetOutcomes["target-a"].ShouldBe("erased");
    }

    [Fact]
    public async Task BeginEraseAsync_DifferentOperationWhileErasing_ConflictsWithoutStateChange() {
        (ProjectionLifecycleActor actor, InMemoryStateManager stateManager) = CreateActor();
        _ = await actor.BeginEraseAsync(new ProjectionEraseBeginRequest("op-1", "digest-1"));

        ProjectionEraseAdmission admission = await actor.BeginEraseAsync(new ProjectionEraseBeginRequest("op-2", "digest-2"));

        admission.Kind.ShouldBe(ProjectionEraseAdmissionKind.Conflict);
        admission.PerTargetOutcomes.ShouldBeEmpty();

        ProjectionLifecycleActorState persisted = PersistedState(stateManager);
        persisted.Phase.ShouldBe(ProjectionLifecyclePhase.Erasing);
        persisted.OperationId.ShouldBe("op-1");
        persisted.ManifestDigest.ShouldBe("digest-1");
    }

    [Fact]
    public async Task RecordTargetOutcomeAsync_MatchingOperation_UpsertsAndPersists() {
        (ProjectionLifecycleActor actor, InMemoryStateManager stateManager) = CreateActor();
        _ = await actor.BeginEraseAsync(new ProjectionEraseBeginRequest("op-1", "digest-1"));

        (await actor.RecordTargetOutcomeAsync(new ProjectionTargetOutcomeRequest("op-1", "target-a", "pending"))).ShouldBeTrue();
        (await actor.RecordTargetOutcomeAsync(new ProjectionTargetOutcomeRequest("op-1", "target-a", "erased"))).ShouldBeTrue();

        ProjectionLifecycleActorState persisted = PersistedState(stateManager);
        persisted.PerTargetOutcomes.Count.ShouldBe(1);
        persisted.PerTargetOutcomes["target-a"].ShouldBe("erased");
    }

    [Fact]
    public async Task RecordTargetOutcomeAsync_WrongOperation_RejectsWithoutMutation() {
        (ProjectionLifecycleActor actor, _) = CreateActor();
        _ = await actor.BeginEraseAsync(new ProjectionEraseBeginRequest("op-1", "digest-1"));

        bool recorded = await actor.RecordTargetOutcomeAsync(new ProjectionTargetOutcomeRequest("op-2", "target-a", "erased"));

        recorded.ShouldBeFalse();
        ProjectionEraseAdmission resume = await actor.BeginEraseAsync(new ProjectionEraseBeginRequest("op-1", "digest-1"));
        resume.PerTargetOutcomes.ShouldBeEmpty();
    }

    [Fact]
    public async Task RecordTargetOutcomeAsync_WhenIdle_ReturnsFalse() {
        (ProjectionLifecycleActor actor, _) = CreateActor();

        bool recorded = await actor.RecordTargetOutcomeAsync(new ProjectionTargetOutcomeRequest("op-1", "target-a", "erased"));

        recorded.ShouldBeFalse();
    }

    [Fact]
    public async Task CompleteEraseAsync_MatchingOperation_ReturnsTrueAndResetsToIdle() {
        (ProjectionLifecycleActor actor, InMemoryStateManager stateManager) = CreateActor();
        _ = await actor.BeginEraseAsync(new ProjectionEraseBeginRequest("op-1", "digest-1"));

        bool completed = await actor.CompleteEraseAsync(new ProjectionEraseCompleteRequest("op-1"));

        completed.ShouldBeTrue();
        ProjectionLifecycleActorState persisted = PersistedState(stateManager);
        persisted.Phase.ShouldBe(ProjectionLifecyclePhase.Idle);
        persisted.OperationId.ShouldBeNull();
        persisted.ManifestDigest.ShouldBeNull();
        persisted.PerTargetOutcomes.ShouldBeEmpty();

        ProjectionDeliveryAdmission delivery = await actor.TryAdmitDeliveryWriteAsync();
        delivery.Admitted.ShouldBeTrue();
        delivery.Phase.ShouldBe(ProjectionLifecyclePhase.Idle);
    }

    [Fact]
    public async Task CompleteEraseAsync_WrongOperation_ReturnsFalseAndStaysErasing() {
        (ProjectionLifecycleActor actor, _) = CreateActor();
        _ = await actor.BeginEraseAsync(new ProjectionEraseBeginRequest("op-1", "digest-1"));

        bool completed = await actor.CompleteEraseAsync(new ProjectionEraseCompleteRequest("op-2"));

        completed.ShouldBeFalse();
        ProjectionDeliveryAdmission delivery = await actor.TryAdmitDeliveryWriteAsync();
        delivery.Admitted.ShouldBeFalse();
        delivery.Phase.ShouldBe(ProjectionLifecyclePhase.Erasing);
    }

    [Fact]
    public async Task TryAdmitDeliveryWriteAsync_WhenIdle_AdmitsWithoutPersistence() {
        (ProjectionLifecycleActor actor, InMemoryStateManager stateManager) = CreateActor();

        ProjectionDeliveryAdmission delivery = await actor.TryAdmitDeliveryWriteAsync();

        delivery.Admitted.ShouldBeTrue();
        delivery.Phase.ShouldBe(ProjectionLifecyclePhase.Idle);
        stateManager.CommittedState.ShouldNotContainKey(StateKey);
    }

    [Fact]
    public async Task TryAdmitDeliveryWriteAsync_WhileErasing_Defers() {
        (ProjectionLifecycleActor actor, _) = CreateActor();
        _ = await actor.BeginEraseAsync(new ProjectionEraseBeginRequest("op-1", "digest-1"));

        ProjectionDeliveryAdmission delivery = await actor.TryAdmitDeliveryWriteAsync();

        delivery.Admitted.ShouldBeFalse();
        delivery.Phase.ShouldBe(ProjectionLifecyclePhase.Erasing);
    }
}
