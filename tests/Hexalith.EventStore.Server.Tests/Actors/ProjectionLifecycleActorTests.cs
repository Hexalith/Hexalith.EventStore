using Dapr.Actors;
using Dapr.Actors.Runtime;

using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Tests.TestUtilities;

using Microsoft.Extensions.Logging;

using NSubstitute;

using Shouldly;

using InMemoryStateManager = Hexalith.EventStore.Testing.Fakes.InMemoryStateManager;

namespace Hexalith.EventStore.Server.Tests.Actors;

/// <summary>
/// Tests for <see cref="ProjectionLifecycleActor"/> covering the erase/deliver admission
/// state machine and its per-turn persistence.
/// </summary>
public class ProjectionLifecycleActorTests {
    private const string StateKey = "projection-lifecycle";

    private static (ProjectionLifecycleActor Actor, InMemoryStateManager StateManager) CreateActor() {
        var stateManager = new InMemoryStateManager();
        ILogger<ProjectionLifecycleActor> logger = Substitute.For<ILogger<ProjectionLifecycleActor>>();
        var host = ActorHost.CreateForTest<ProjectionLifecycleActor>(
            new ActorTestOptions { ActorId = new ActorId("tenant:domain:agg:counter") });
        var actor = new ProjectionLifecycleActor(host, logger);

        ActorStateManagerTestHelper.SetStateManager(actor, stateManager);

        return (actor, stateManager);
    }

    private static ProjectionLifecycleActorState PersistedState(InMemoryStateManager stateManager) {
        stateManager.CommittedState.ShouldContainKey(StateKey);
        return (ProjectionLifecycleActorState)stateManager.CommittedState[StateKey];
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
