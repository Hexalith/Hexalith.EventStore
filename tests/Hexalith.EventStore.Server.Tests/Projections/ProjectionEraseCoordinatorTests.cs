using System.Reflection;

using Dapr;
using Dapr.Client;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Projections;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Projections;

/// <summary>
/// Story 1.9 Task 6: the coordinated, resumable, structured-outcome projection eraser
/// (<see cref="ProjectionEraseCoordinator"/>). Every collaborator is a substitute so the coordinator's
/// ordering, classification, gating, resume, and completion semantics are exercised deterministically.
/// </summary>
public class ProjectionEraseCoordinatorTests {
    private const string TenantId = "tenant-a";
    private const string Domain = "party";
    private const string AggregateId = "party-1";
    private const string ProjectionName = "party-summary";
    private const string Slot = "primary";
    private const string OperationId = "01HX0000000000000000000000";
    private const string RmStore = "readmodel-store";
    private const string RmKey = "readmodel:tenant-a:party:party-summary:party-1:primary";

    private static readonly AggregateIdentity Identity = new(TenantId, Domain, AggregateId);
    private static readonly string RebuildKey = ProjectionRebuildCheckpointStore.GetStateKey(
        new ProjectionRebuildCheckpointScope(TenantId, Domain, ProjectionName, AggregateId, OperationId));
    private static readonly string DeliveryKey = ProjectionCheckpointTracker.GetProjectionScopedStateKey(Identity, ProjectionName);

    private readonly IProjectionReadModelAddressFactory _addressFactory = Substitute.For<IProjectionReadModelAddressFactory>();
    private readonly IReadModelConditionalEraser _readModelEraser = Substitute.For<IReadModelConditionalEraser>();
    private readonly IProjectionRebuildCheckpointEraser _rebuildEraser = Substitute.For<IProjectionRebuildCheckpointEraser>();
    private readonly IProjectionDeliveryCheckpointStore _deliveryStore = Substitute.For<IProjectionDeliveryCheckpointStore>();
    private readonly IProjectionRebuildCheckpointStore _rebuildStore = Substitute.For<IProjectionRebuildCheckpointStore>();
    private readonly IProjectionLifecycleGateway _gateway = Substitute.For<IProjectionLifecycleGateway>();

    public ProjectionEraseCoordinatorTests() {
        // Default happy defaults: one aggregate-owned slot, no active rebuild, freshly admitted, all
        // record/complete calls acknowledged.
        _ = _addressFactory.Create(Arg.Any<AggregateIdentity>(), ProjectionName, Slot)
            .Returns(new ProjectionReadModelAddress(RmStore, RmKey, TenantId, Domain, ProjectionName, AggregateId, Slot));
        _ = _rebuildStore.HasActiveOperatorRebuildForDomainAsync(TenantId, Domain, Arg.Any<CancellationToken>())
            .Returns(false);
        _ = _gateway.BeginEraseAsync(Arg.Any<AggregateIdentity>(), ProjectionName, OperationId, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Admission(ProjectionEraseAdmissionKind.Admitted));
        _ = _gateway.RecordTargetOutcomeAsync(Arg.Any<AggregateIdentity>(), ProjectionName, OperationId, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _ = _gateway.CompleteEraseAsync(Arg.Any<AggregateIdentity>(), ProjectionName, OperationId, Arg.Any<CancellationToken>())
            .Returns(true);
    }

    [Fact]
    public async Task EraseAsync_AllTargetsPresentThenErased_ReturnsSuccessInOrderAndCompletesOnce() {
        SetupReadModel((true, "etag-rm"), (false, ""), (false, ""));
        _ = _readModelEraser.TryEraseAsync(RmStore, RmKey, "etag-rm", Arg.Any<CancellationToken>()).Returns(true);
        SetupRebuild((true, "etag-rb"), (false, ""), (false, ""));
        _ = _rebuildEraser.TryEraseAggregateCheckpointAsync(Arg.Any<ProjectionRebuildCheckpointScope>(), "etag-rb", Arg.Any<CancellationToken>()).Returns(true);
        SetupDelivery((true, "etag-dl"), (false, ""), (false, ""));
        _ = _deliveryStore.TryEraseAsync(Identity, ProjectionName, "etag-dl", Arg.Any<CancellationToken>()).Returns(true);

        ProjectionEraseResult result = await CreateSut().EraseAsync(Request());

        result.Kind.ShouldBe(ProjectionEraseOutcomeKind.Success);
        result.TargetOutcomes.Select(o => o.TargetKey).ShouldBe([RmKey, RebuildKey, DeliveryKey]);
        result.TargetOutcomes.ShouldAllBe(o => o.Outcome == "Complete");
        _ = await _gateway.Received(1).RecordTargetOutcomeAsync(Arg.Any<AggregateIdentity>(), ProjectionName, OperationId, RmKey, "Complete", Arg.Any<CancellationToken>());
        _ = await _gateway.Received(1).RecordTargetOutcomeAsync(Arg.Any<AggregateIdentity>(), ProjectionName, OperationId, RebuildKey, "Complete", Arg.Any<CancellationToken>());
        _ = await _gateway.Received(1).RecordTargetOutcomeAsync(Arg.Any<AggregateIdentity>(), ProjectionName, OperationId, DeliveryKey, "Complete", Arg.Any<CancellationToken>());
        _ = await _gateway.Received(1).CompleteEraseAsync(Arg.Any<AggregateIdentity>(), ProjectionName, OperationId, Arg.Any<CancellationToken>());

        Received.InOrder(() => {
            _ = _gateway.BeginEraseAsync(Arg.Any<AggregateIdentity>(), ProjectionName, OperationId, Arg.Any<string>(), Arg.Any<CancellationToken>());
            _ = _gateway.RecordTargetOutcomeAsync(Arg.Any<AggregateIdentity>(), ProjectionName, OperationId, RmKey, "Complete", Arg.Any<CancellationToken>());
            _ = _gateway.RecordTargetOutcomeAsync(Arg.Any<AggregateIdentity>(), ProjectionName, OperationId, RebuildKey, "Complete", Arg.Any<CancellationToken>());
            _ = _gateway.RecordTargetOutcomeAsync(Arg.Any<AggregateIdentity>(), ProjectionName, OperationId, DeliveryKey, "Complete", Arg.Any<CancellationToken>());
            _ = _gateway.CompleteEraseAsync(Arg.Any<AggregateIdentity>(), ProjectionName, OperationId, Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task EraseAsync_EmptySlots_ReturnsUnsupportedWithoutMutation() {
        ProjectionEraseResult result = await CreateSut().EraseAsync(Request(slots: []));

        result.Kind.ShouldBe(ProjectionEraseOutcomeKind.Unsupported);
        result.ReasonCode.ShouldBe("no-slots");
        await AssertNoMutationAsync();
    }

    [Fact]
    public async Task EraseAsync_UnregisteredOrSharedSlot_ReturnsUnsupportedBeforeMutation() {
        _ = _addressFactory.Create(Arg.Any<AggregateIdentity>(), ProjectionName, Slot)
            .Returns<ProjectionReadModelAddress>(_ => throw new ProjectionReadModelAddressException("not registered"));

        ProjectionEraseResult result = await CreateSut().EraseAsync(Request());

        result.Kind.ShouldBe(ProjectionEraseOutcomeKind.Unsupported);
        result.ReasonCode.ShouldBe("unresolvable-target");
        _ = await _rebuildStore.DidNotReceive().HasActiveOperatorRebuildForDomainAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await AssertNoMutationAsync();
    }

    [Fact]
    public async Task EraseAsync_ActiveRebuild_ReturnsActiveRebuildWithoutMutation() {
        _ = _rebuildStore.HasActiveOperatorRebuildForDomainAsync(TenantId, Domain, Arg.Any<CancellationToken>()).Returns(true);

        ProjectionEraseResult result = await CreateSut().EraseAsync(Request());

        result.Kind.ShouldBe(ProjectionEraseOutcomeKind.ActiveRebuild);
        result.ReasonCode.ShouldBe("active-rebuild");
        await AssertNoMutationAsync();
    }

    [Fact]
    public async Task EraseAsync_ActiveRebuildGateThrows_TreatsAsActiveRebuildFailClosed() {
        _ = _rebuildStore.HasActiveOperatorRebuildForDomainAsync(TenantId, Domain, Arg.Any<CancellationToken>())
            .Returns<Task<bool>>(_ => throw new DaprException("gate unavailable"));

        ProjectionEraseResult result = await CreateSut().EraseAsync(Request());

        result.Kind.ShouldBe(ProjectionEraseOutcomeKind.ActiveRebuild);
        result.ReasonCode.ShouldBe("active-rebuild-gate-unavailable");
        await AssertNoMutationAsync();
    }

    [Fact]
    public async Task EraseAsync_BeginEraseConflict_ReturnsConflictWithoutEraseOrComplete() {
        _ = _gateway.BeginEraseAsync(Arg.Any<AggregateIdentity>(), ProjectionName, OperationId, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Admission(ProjectionEraseAdmissionKind.Conflict));

        ProjectionEraseResult result = await CreateSut().EraseAsync(Request());

        result.Kind.ShouldBe(ProjectionEraseOutcomeKind.Conflict);
        result.ReasonCode.ShouldBe("operation-conflict");
        _ = await _readModelEraser.DidNotReceive().TryEraseAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        _ = await _rebuildEraser.DidNotReceive().TryEraseAggregateCheckpointAsync(Arg.Any<ProjectionRebuildCheckpointScope>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        _ = await _gateway.DidNotReceive().RecordTargetOutcomeAsync(Arg.Any<AggregateIdentity>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        _ = await _gateway.DidNotReceive().CompleteEraseAsync(Arg.Any<AggregateIdentity>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EraseAsync_ReadModelConditionalEraseReturnsFalse_ReturnsConflictAndDoesNotDeleteNewerValue() {
        SetupReadModel((true, "etag-old"));
        _ = _readModelEraser.TryEraseAsync(RmStore, RmKey, "etag-old", Arg.Any<CancellationToken>()).Returns(false);
        // Rebuild + delivery are absent, so only the read-model conflict drives the aggregate outcome.

        ProjectionEraseResult result = await CreateSut().EraseAsync(Request());

        result.Kind.ShouldBe(ProjectionEraseOutcomeKind.Conflict);
        result.ReasonCode.ShouldBe("target-conflict");
        result.TargetOutcomes.Single(o => o.TargetKey == RmKey).Outcome.ShouldBe("Conflict");
        // The conditional erase was attempted once with the read ETag and refused; no forced re-delete.
        _ = await _readModelEraser.Received(1).TryEraseAsync(RmStore, RmKey, "etag-old", Arg.Any<CancellationToken>());
        _ = await _readModelEraser.DidNotReceive().TryEraseAsync(RmStore, RmKey, Arg.Is<string>(e => e != "etag-old"), Arg.Any<CancellationToken>());
        _ = await _gateway.DidNotReceive().CompleteEraseAsync(Arg.Any<AggregateIdentity>(), ProjectionName, OperationId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EraseAsync_AmbiguousTransportThenReadBackAbsent_ClassifiesComplete() {
        SetupReadModel((true, "etag-rm"), (false, ""), (false, ""));
        _ = _readModelEraser.TryEraseAsync(RmStore, RmKey, "etag-rm", Arg.Any<CancellationToken>())
            .Throws(new DaprException("ambiguous dispatch"));

        ProjectionEraseResult result = await CreateSut().EraseAsync(Request());

        result.Kind.ShouldBe(ProjectionEraseOutcomeKind.Success);
        result.TargetOutcomes.Single(o => o.TargetKey == RmKey).Outcome.ShouldBe("Complete");
        _ = await _gateway.Received(1).RecordTargetOutcomeAsync(Arg.Any<AggregateIdentity>(), ProjectionName, OperationId, RmKey, "Complete", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EraseAsync_AmbiguousTransportThenReadBackSameEtag_ClassifiesIncomplete() {
        SetupReadModel((true, "etag-rm"), (true, "etag-rm"));
        _ = _readModelEraser.TryEraseAsync(RmStore, RmKey, "etag-rm", Arg.Any<CancellationToken>())
            .Throws(new DaprException("ambiguous dispatch"));

        ProjectionEraseResult result = await CreateSut().EraseAsync(Request());

        result.Kind.ShouldBe(ProjectionEraseOutcomeKind.Incomplete);
        result.ReasonCode.ShouldBe("target-incomplete");
        result.TargetOutcomes.Single(o => o.TargetKey == RmKey).Outcome.ShouldBe("Incomplete");
        _ = await _gateway.DidNotReceive().CompleteEraseAsync(Arg.Any<AggregateIdentity>(), ProjectionName, OperationId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EraseAsync_AmbiguousTransportThenReadBackChangedEtag_ClassifiesConflict() {
        SetupReadModel((true, "etag-rm"), (true, "etag-newer"));
        _ = _readModelEraser.TryEraseAsync(RmStore, RmKey, "etag-rm", Arg.Any<CancellationToken>())
            .Throws(new DaprException("ambiguous dispatch"));

        ProjectionEraseResult result = await CreateSut().EraseAsync(Request());

        result.Kind.ShouldBe(ProjectionEraseOutcomeKind.Conflict);
        result.TargetOutcomes.Single(o => o.TargetKey == RmKey).Outcome.ShouldBe("Conflict");
        _ = await _gateway.DidNotReceive().CompleteEraseAsync(Arg.Any<AggregateIdentity>(), ProjectionName, OperationId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EraseAsync_ResumeWithTargetAlreadyComplete_SkipsCompletedTarget() {
        _ = _gateway.BeginEraseAsync(Arg.Any<AggregateIdentity>(), ProjectionName, OperationId, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Admission(ProjectionEraseAdmissionKind.Resume, new Dictionary<string, string>(StringComparer.Ordinal) {
                [RmKey] = "Complete",
            }));
        // The read-model target was already erased in the prior attempt, so it verifies absent.
        SetupReadModel((false, ""));
        // Rebuild + delivery are absent -> Complete, so the whole operation completes.

        ProjectionEraseResult result = await CreateSut().EraseAsync(Request());

        result.Kind.ShouldBe(ProjectionEraseOutcomeKind.Success);
        // Skipped target: no re-erase and no re-record for the already-complete key.
        _ = await _readModelEraser.DidNotReceive().TryEraseAsync(RmStore, RmKey, Arg.Any<string>(), Arg.Any<CancellationToken>());
        _ = await _gateway.DidNotReceive().RecordTargetOutcomeAsync(Arg.Any<AggregateIdentity>(), ProjectionName, OperationId, RmKey, Arg.Any<string>(), Arg.Any<CancellationToken>());
        _ = await _gateway.Received(1).CompleteEraseAsync(Arg.Any<AggregateIdentity>(), ProjectionName, OperationId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EraseAsync_VerifyAbsentFindsTargetPresent_ReturnsIncompleteWithoutComplete() {
        // Classify to Complete (erase succeeds + read-back absent), but the verify re-read finds it present.
        SetupReadModel((true, "etag-rm"), (false, ""), (true, "etag-reappeared"));
        _ = _readModelEraser.TryEraseAsync(RmStore, RmKey, "etag-rm", Arg.Any<CancellationToken>()).Returns(true);

        ProjectionEraseResult result = await CreateSut().EraseAsync(Request());

        result.Kind.ShouldBe(ProjectionEraseOutcomeKind.Incomplete);
        result.ReasonCode.ShouldBe("verify-present");
        _ = await _gateway.DidNotReceive().CompleteEraseAsync(Arg.Any<AggregateIdentity>(), ProjectionName, OperationId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EraseAsync_CancelledMidErase_ReturnsCanceledAndNeverCompletes() {
        _ = _readModelEraser.TryReadEtagAsync(RmStore, RmKey, Arg.Any<CancellationToken>())
            .Throws(new OperationCanceledException());

        ProjectionEraseResult result = await CreateSut().EraseAsync(Request());

        result.Kind.ShouldBe(ProjectionEraseOutcomeKind.Canceled);
        result.Kind.ShouldNotBe(ProjectionEraseOutcomeKind.Success);
        _ = await _gateway.DidNotReceive().CompleteEraseAsync(Arg.Any<AggregateIdentity>(), ProjectionName, OperationId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EraseAsync_CapabilityNotOptedIn_ReturnsUnsupportedBeforeBegin() {
        var sut = new ProjectionEraseCoordinator(
            _addressFactory,
            _rebuildStore,
            _gateway,
            NullLogger<ProjectionEraseCoordinator>.Instance,
            readModelEraser: null,
            rebuildEraser: _rebuildEraser,
            deliveryCheckpointStore: _deliveryStore);

        ProjectionEraseResult result = await sut.EraseAsync(Request());

        result.Kind.ShouldBe(ProjectionEraseOutcomeKind.Unsupported);
        result.ReasonCode.ShouldBe("capability-unavailable");
        _ = await _gateway.DidNotReceive().BeginEraseAsync(Arg.Any<AggregateIdentity>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Coordinator_HasNoDaprClientDependency_SoStateTransactionsAreStructurallyImpossible() {
        // The coordinator only depends on single-key TryErase capabilities; it never receives a DaprClient,
        // so ExecuteStateTransactionAsync can never be invoked on the erase path (resumable-only).
        bool hasDaprClient = typeof(ProjectionEraseCoordinator)
            .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .SelectMany(c => c.GetParameters())
            .Any(p => p.ParameterType == typeof(DaprClient));

        hasDaprClient.ShouldBeFalse();
    }

    private static ProjectionEraseAdmission Admission(
        ProjectionEraseAdmissionKind kind,
        IReadOnlyDictionary<string, string>? outcomes = null)
        => new(kind, outcomes ?? new Dictionary<string, string>(StringComparer.Ordinal));

    private static ProjectionEraseRequest Request(IReadOnlyList<string>? slots = null)
        => new(TenantId, Domain, AggregateId, ProjectionName, slots ?? [Slot], OperationId);

    private ProjectionEraseCoordinator CreateSut()
        => new(
            _addressFactory,
            _rebuildStore,
            _gateway,
            NullLogger<ProjectionEraseCoordinator>.Instance,
            _readModelEraser,
            _rebuildEraser,
            _deliveryStore);

    private void SetupReadModel((bool Present, string Etag) first, params (bool Present, string Etag)[] rest)
        => _ = _readModelEraser.TryReadEtagAsync(RmStore, RmKey, Arg.Any<CancellationToken>()).Returns(first, rest);

    private void SetupRebuild((bool Present, string Etag) first, params (bool Present, string Etag)[] rest)
        => _ = _rebuildEraser.TryReadAggregateCheckpointEtagAsync(Arg.Any<ProjectionRebuildCheckpointScope>(), Arg.Any<CancellationToken>()).Returns(first, rest);

    private void SetupDelivery((bool Present, string Etag) first, params (bool Present, string Etag)[] rest)
        => _ = _deliveryStore.TryReadDeliveryCheckpointEtagAsync(Identity, ProjectionName, Arg.Any<CancellationToken>()).Returns(first, rest);

    private async Task AssertNoMutationAsync() {
        _ = await _gateway.DidNotReceive().BeginEraseAsync(Arg.Any<AggregateIdentity>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        _ = await _gateway.DidNotReceive().RecordTargetOutcomeAsync(Arg.Any<AggregateIdentity>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        _ = await _gateway.DidNotReceive().CompleteEraseAsync(Arg.Any<AggregateIdentity>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        _ = await _readModelEraser.DidNotReceive().TryEraseAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        _ = await _rebuildEraser.DidNotReceive().TryEraseAggregateCheckpointAsync(Arg.Any<ProjectionRebuildCheckpointScope>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        _ = await _deliveryStore.DidNotReceive().TryEraseAsync(Arg.Any<AggregateIdentity>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
