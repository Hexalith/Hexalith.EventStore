using System.Security.Claims;
using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.Controllers;
using Hexalith.EventStore.ErrorHandling;
using Hexalith.EventStore.Server.Projections;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Controllers;

public class AdminProjectionRebuildControllerTests {
    private const string Tenant = "tenant-a";
    private const string Projection = "party-summary";

    [Fact]
    public async Task ReplayProjectionStartsRunningOperationAndReturnsAcceptedResult() {
        IProjectionRebuildCheckpointStore store = Substitute.For<IProjectionRebuildCheckpointStore>();
        _ = store.ResetAsync(
                Arg.Any<ProjectionRebuildCheckpointScope>(),
                9,
                ProjectionRebuildStatus.Running,
                null,
                Arg.Any<CancellationToken>(),
                20)
            .Returns(ProjectionRebuildCheckpointSaveResult.Success(CreateCheckpoint(9, ProjectionRebuildStatus.Running, "01HX0000000000000000000000", toPosition: 20)));
        AdminProjectionRebuildController controller = CreateController(store, asGlobalAdmin: true);

        IActionResult result = await controller.ReplayProjection(
            Tenant,
            Projection,
            new ProjectionReplayRequest(10, 20));

        AcceptedResult accepted = result.ShouldBeOfType<AcceptedResult>();
        accepted.StatusCode.ShouldBe(StatusCodes.Status202Accepted);
        AdminOperationResult operation = accepted.Value.ShouldBeOfType<AdminOperationResult>();
        operation.Success.ShouldBeTrue();
        operation.OperationId.Length.ShouldBe(26);
        _ = await store.Received(1).ResetAsync(
            Arg.Is<ProjectionRebuildCheckpointScope>(scope =>
                scope.Tenant == Tenant
                && scope.Domain == Projection
                && scope.ProjectionName == Projection
                && !string.IsNullOrWhiteSpace(scope.OperationId)),
            9,
            ProjectionRebuildStatus.Running,
            null,
            Arg.Any<CancellationToken>(),
            20);
    }

    [Fact]
    public async Task ReplayProjectionInvokesRebuildOrchestratorAfterCheckpointStart() {
        IProjectionRebuildCheckpointStore store = Substitute.For<IProjectionRebuildCheckpointStore>();
        _ = store.ResetAsync(
                Arg.Any<ProjectionRebuildCheckpointScope>(),
                9,
                ProjectionRebuildStatus.Running,
                null,
                Arg.Any<CancellationToken>(),
                20)
            .Returns(ProjectionRebuildCheckpointSaveResult.Success(CreateCheckpoint(9, ProjectionRebuildStatus.Running, "01HX0000000000000000000000", toPosition: 20)));
        IProjectionRebuildOrchestrator rebuildOrchestrator = Substitute.For<IProjectionRebuildOrchestrator>();
        AdminProjectionRebuildController controller = CreateController(store, asGlobalAdmin: true, rebuildOrchestrator);

        _ = await controller.ReplayProjection(
            Tenant,
            Projection,
            new ProjectionReplayRequest(10, 20));

        await rebuildOrchestrator.Received(1).RebuildProjectionAsync(
            Arg.Is<ProjectionRebuildCheckpointScope>(scope =>
                scope.Tenant == Tenant
                && scope.Domain == Projection
                && scope.ProjectionName == Projection
                && !string.IsNullOrWhiteSpace(scope.OperationId)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PauseProjectionTransitionsExistingRebuildAndReturnsOkResult() {
        IProjectionRebuildCheckpointStore store = Substitute.For<IProjectionRebuildCheckpointStore>();
        ProjectionRebuildCheckpoint existing = CreateCheckpoint(50, ProjectionRebuildStatus.Running, toPosition: 90);
        _ = store.ReadAsync(Arg.Any<ProjectionRebuildCheckpointScope>(), Arg.Any<CancellationToken>())
            .Returns(existing);
        _ = store.SaveAsync(
                Arg.Any<ProjectionRebuildCheckpointScope>(),
                50,
                ProjectionRebuildStatus.Paused,
                null,
                Arg.Any<CancellationToken>(),
                90)
            .Returns(ProjectionRebuildCheckpointSaveResult.Success(CreateCheckpoint(50, ProjectionRebuildStatus.Paused, toPosition: 90)));
        AdminProjectionRebuildController controller = CreateController(store, asGlobalAdmin: true);

        IActionResult result = await controller.PauseProjection(Tenant, Projection);

        OkObjectResult ok = result.ShouldBeOfType<OkObjectResult>();
        AdminOperationResult operation = ok.Value.ShouldBeOfType<AdminOperationResult>();
        operation.Success.ShouldBeTrue();
        operation.ErrorCode.ShouldBeNull();
        _ = await store.Received(1).SaveAsync(
            Arg.Any<ProjectionRebuildCheckpointScope>(),
            50,
            ProjectionRebuildStatus.Paused,
            null,
            Arg.Any<CancellationToken>(),
            90);
    }

    [Fact]
    public async Task PauseProjectionWithoutExistingRebuildReturnsRebuildNotFound() {
        IProjectionRebuildCheckpointStore store = Substitute.For<IProjectionRebuildCheckpointStore>();
        _ = store.ReadAsync(Arg.Any<ProjectionRebuildCheckpointScope>(), Arg.Any<CancellationToken>())
            .Returns((ProjectionRebuildCheckpoint?)null);
        AdminProjectionRebuildController controller = CreateController(store, asGlobalAdmin: true);

        IActionResult result = await controller.PauseProjection(Tenant, Projection);

        ObjectResult problemResult = result.ShouldBeOfType<ObjectResult>();
        problemResult.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
        ProblemDetails problem = problemResult.Value.ShouldBeOfType<ProblemDetails>();
        problem.Extensions["reasonCode"].ShouldBe(StreamReplayReasonCodes.RebuildOperationNotFound);
        _ = await store.DidNotReceive().SaveAsync(
            Arg.Any<ProjectionRebuildCheckpointScope>(),
            Arg.Any<long>(),
            Arg.Any<ProjectionRebuildStatus>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReplayProjectionCheckpointConflictReturnsProblemWithReasonCode() {
        IProjectionRebuildCheckpointStore store = Substitute.For<IProjectionRebuildCheckpointStore>();
        _ = store.ResetAsync(
                Arg.Any<ProjectionRebuildCheckpointScope>(),
                Arg.Any<long>(),
                Arg.Any<ProjectionRebuildStatus>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>(),
                Arg.Any<long?>())
            .Returns(ProjectionRebuildCheckpointSaveResult.Failure(StreamReplayReasonCodes.CheckpointConflict));
        AdminProjectionRebuildController controller = CreateController(store, asGlobalAdmin: true);

        IActionResult result = await controller.ReplayProjection(
            Tenant,
            Projection,
            new ProjectionReplayRequest(10, 20));

        ObjectResult problemResult = result.ShouldBeOfType<ObjectResult>();
        problemResult.StatusCode.ShouldBe(StatusCodes.Status409Conflict);
        ProblemDetails problem = problemResult.Value.ShouldBeOfType<ProblemDetails>();
        problem.Extensions["reasonCode"].ShouldBe(StreamReplayReasonCodes.CheckpointConflict);
    }

    [Fact]
    public async Task GetRebuildStatusWithoutCheckpointReturnsNotStartedOperation() {
        IProjectionRebuildCheckpointStore store = Substitute.For<IProjectionRebuildCheckpointStore>();
        AdminProjectionRebuildController controller = CreateController(store, asGlobalAdmin: true);

        IActionResult result = await controller.GetRebuildStatus(Tenant, Projection);

        OkObjectResult ok = result.ShouldBeOfType<OkObjectResult>();
        ProjectionRebuildOperation operation = ok.Value.ShouldBeOfType<ProjectionRebuildOperation>();
        operation.Status.ShouldBe(ProjectionRebuildStatus.NotStarted);
        operation.OperationId.ShouldBeEmpty();
        operation.StartedAt.ShouldBeNull();
    }

    [Fact]
    public async Task ReplayProjectionWithoutGlobalAdministratorRoleReturnsForbidden() {
        IProjectionRebuildCheckpointStore store = Substitute.For<IProjectionRebuildCheckpointStore>();
        AdminProjectionRebuildController controller = CreateController(store, asGlobalAdmin: false);

        IActionResult result = await controller.ReplayProjection(
            Tenant,
            Projection,
            new ProjectionReplayRequest(10, 20));

        ObjectResult problemResult = result.ShouldBeOfType<ObjectResult>();
        problemResult.StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
        ProblemDetails problem = problemResult.Value.ShouldBeOfType<ProblemDetails>();
        problem.Type.ShouldBe(ProblemTypeUris.Forbidden);
        problem.Extensions["reasonCode"].ShouldBe(StreamReplayReasonCodes.ForbiddenRole);
        AssertNoForbiddenLeakage(problem);
        _ = await store.DidNotReceiveWithAnyArgs().SaveAsync(default!, default, default, default, default);
        _ = await store.DidNotReceiveWithAnyArgs().ReadAsync(default!, default);
    }

    [Fact]
    public async Task ResetProjectionUsesExplicitRewindAndFreshOperationId() {
        IProjectionRebuildCheckpointStore store = Substitute.For<IProjectionRebuildCheckpointStore>();
        _ = store.ResetAsync(
                Arg.Any<ProjectionRebuildCheckpointScope>(),
                0,
                ProjectionRebuildStatus.NotStarted,
                null,
                Arg.Any<CancellationToken>(),
                null)
            .Returns(ProjectionRebuildCheckpointSaveResult.Success(CreateCheckpoint(0, ProjectionRebuildStatus.NotStarted, "01HX0000000000000000000001")));
        AdminProjectionRebuildController controller = CreateController(store, asGlobalAdmin: true);

        IActionResult result = await controller.ResetProjection(Tenant, Projection, new ProjectionResetRequest(0));

        AcceptedResult accepted = result.ShouldBeOfType<AcceptedResult>();
        AdminOperationResult operation = accepted.Value.ShouldBeOfType<AdminOperationResult>();
        operation.OperationId.Length.ShouldBe(26);
        _ = await store.Received(1).ResetAsync(
            Arg.Is<ProjectionRebuildCheckpointScope>(scope => !string.IsNullOrWhiteSpace(scope.OperationId)),
            0,
            ProjectionRebuildStatus.NotStarted,
            null,
            Arg.Any<CancellationToken>(),
            null);
    }

    [Fact]
    public async Task PauseProjectionWithoutGlobalAdministratorRoleReturnsForbiddenBeforeStoreAccess() {
        IProjectionRebuildCheckpointStore store = Substitute.For<IProjectionRebuildCheckpointStore>();
        AdminProjectionRebuildController controller = CreateController(store, asGlobalAdmin: false);

        IActionResult result = await controller.PauseProjection(Tenant, Projection);

        ObjectResult problemResult = result.ShouldBeOfType<ObjectResult>();
        problemResult.StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
        ProblemDetails problem = problemResult.Value.ShouldBeOfType<ProblemDetails>();
        problem.Extensions["reasonCode"].ShouldBe(StreamReplayReasonCodes.ForbiddenRole);
        AssertNoForbiddenLeakage(problem);
        _ = await store.DidNotReceiveWithAnyArgs().ReadAsync(default!, default);
        _ = await store.DidNotReceiveWithAnyArgs().SaveAsync(default!, default, default, default, default);
    }

    [Fact]
    public async Task RetryProjectionUsesResetAsyncToPreserveFailedLifecycleBoundary() {
        IProjectionRebuildCheckpointStore store = Substitute.For<IProjectionRebuildCheckpointStore>();
        ProjectionRebuildCheckpoint existing = CreateCheckpoint(
            25,
            ProjectionRebuildStatus.Failed,
            failureReasonCode: "domain-failure",
            toPosition: 50);
        _ = store.ReadAsync(Arg.Any<ProjectionRebuildCheckpointScope>(), Arg.Any<CancellationToken>())
            .Returns(existing);
        _ = store.ResetAsync(
                Arg.Any<ProjectionRebuildCheckpointScope>(),
                25,
                ProjectionRebuildStatus.Retrying,
                "domain-failure",
                Arg.Any<CancellationToken>(),
                50)
            .Returns(ProjectionRebuildCheckpointSaveResult.Success(CreateCheckpoint(25, ProjectionRebuildStatus.Retrying, toPosition: 50)));
        AdminProjectionRebuildController controller = CreateController(store, asGlobalAdmin: true);

        IActionResult result = await controller.RetryProjection(Tenant, Projection);

        AcceptedResult accepted = result.ShouldBeOfType<AcceptedResult>();
        accepted.StatusCode.ShouldBe(StatusCodes.Status202Accepted);
        _ = await store.Received(1).ResetAsync(
            Arg.Any<ProjectionRebuildCheckpointScope>(),
            25,
            ProjectionRebuildStatus.Retrying,
            "domain-failure",
            Arg.Any<CancellationToken>(),
            50);
        _ = await store.DidNotReceiveWithAnyArgs().SaveAsync(default!, default, default, default, default);
    }

    [Fact]
    public async Task RetryProjectionInvokesRebuildOrchestratorAfterCheckpointStart() {
        IProjectionRebuildCheckpointStore store = Substitute.For<IProjectionRebuildCheckpointStore>();
        ProjectionRebuildCheckpoint existing = CreateCheckpoint(25, ProjectionRebuildStatus.Failed, failureReasonCode: "domain-failure", toPosition: 50);
        _ = store.ReadAsync(Arg.Any<ProjectionRebuildCheckpointScope>(), Arg.Any<CancellationToken>())
            .Returns(existing);
        _ = store.ResetAsync(
                Arg.Any<ProjectionRebuildCheckpointScope>(),
                25,
                ProjectionRebuildStatus.Retrying,
                "domain-failure",
                Arg.Any<CancellationToken>(),
                50)
            .Returns(ProjectionRebuildCheckpointSaveResult.Success(CreateCheckpoint(25, ProjectionRebuildStatus.Retrying, toPosition: 50)));
        IProjectionRebuildOrchestrator rebuildOrchestrator = Substitute.For<IProjectionRebuildOrchestrator>();
        AdminProjectionRebuildController controller = CreateController(store, asGlobalAdmin: true, rebuildOrchestrator);

        _ = await controller.RetryProjection(Tenant, Projection);

        await rebuildOrchestrator.Received(1).RebuildProjectionAsync(
            Arg.Is<ProjectionRebuildCheckpointScope>(scope =>
                scope.Tenant == Tenant
                && scope.Domain == Projection
                && scope.ProjectionName == Projection
                && !string.IsNullOrWhiteSpace(scope.OperationId)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReplayProjectionWithRunningTerminalSnapshotReturnsIncompleteAcceptedResult() {
        IProjectionRebuildCheckpointStore store = Substitute.For<IProjectionRebuildCheckpointStore>();
        ProjectionRebuildCheckpoint running = CreateCheckpoint(9, ProjectionRebuildStatus.Running, "01HX0000000000000000000000", toPosition: 20);
        _ = store.ResetAsync(
                Arg.Any<ProjectionRebuildCheckpointScope>(),
                9,
                ProjectionRebuildStatus.Running,
                null,
                Arg.Any<CancellationToken>(),
                20)
            .Returns(ProjectionRebuildCheckpointSaveResult.Success(running));
        _ = store.ReadAsync(Arg.Any<ProjectionRebuildCheckpointScope>(), Arg.Any<CancellationToken>())
            .Returns(running);
        IProjectionRebuildOrchestrator rebuildOrchestrator = Substitute.For<IProjectionRebuildOrchestrator>();
        AdminProjectionRebuildController controller = CreateController(store, asGlobalAdmin: true, rebuildOrchestrator);

        IActionResult result = await controller.ReplayProjection(Tenant, Projection, new ProjectionReplayRequest(10, 20));

        AcceptedResult accepted = result.ShouldBeOfType<AcceptedResult>();
        AdminOperationResult operation = accepted.Value.ShouldBeOfType<AdminOperationResult>();
        operation.Success.ShouldBeFalse();
        operation.ErrorCode.ShouldBe(StreamReplayReasonCodes.OperationInFlight);
    }

    [Fact]
    public async Task ReplayProjectionCheckpointUnavailableAddsRetryAfterHeader() {
        IProjectionRebuildCheckpointStore store = Substitute.For<IProjectionRebuildCheckpointStore>();
        _ = store.ResetAsync(
                Arg.Any<ProjectionRebuildCheckpointScope>(),
                Arg.Any<long>(),
                Arg.Any<ProjectionRebuildStatus>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>(),
                Arg.Any<long?>())
            .Returns(ProjectionRebuildCheckpointSaveResult.Failure(StreamReplayReasonCodes.CheckpointUnavailable));
        AdminProjectionRebuildController controller = CreateController(store, asGlobalAdmin: true);

        IActionResult result = await controller.ReplayProjection(Tenant, Projection, new ProjectionReplayRequest(10, 20));

        ObjectResult problemResult = result.ShouldBeOfType<ObjectResult>();
        problemResult.StatusCode.ShouldBe(StatusCodes.Status503ServiceUnavailable);
        controller.Response.Headers.RetryAfter.ToString().ShouldBe("5");
    }

    [Theory]
    [InlineData(StreamReplayReasonCodes.CheckpointConflict)]
    [InlineData(StreamReplayReasonCodes.StaleCheckpoint)]
    public async Task ReplayProjectionConflictFailuresAddRetryAfterHeader(string reasonCode) {
        IProjectionRebuildCheckpointStore store = Substitute.For<IProjectionRebuildCheckpointStore>();
        _ = store.ResetAsync(
                Arg.Any<ProjectionRebuildCheckpointScope>(),
                Arg.Any<long>(),
                Arg.Any<ProjectionRebuildStatus>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>(),
                Arg.Any<long?>())
            .Returns(ProjectionRebuildCheckpointSaveResult.Failure(reasonCode));
        AdminProjectionRebuildController controller = CreateController(store, asGlobalAdmin: true);

        IActionResult result = await controller.ReplayProjection(Tenant, Projection, new ProjectionReplayRequest(10, 20));

        ObjectResult problemResult = result.ShouldBeOfType<ObjectResult>();
        problemResult.StatusCode.ShouldBe(StatusCodes.Status409Conflict);
        controller.Response.Headers.RetryAfter.ToString().ShouldBe("5");
    }

    [Fact]
    public async Task ReplayProjectionNoDomainServiceAddsLongRetryAfterHeader() {
        IProjectionRebuildCheckpointStore store = Substitute.For<IProjectionRebuildCheckpointStore>();
        _ = store.ResetAsync(
                Arg.Any<ProjectionRebuildCheckpointScope>(),
                Arg.Any<long>(),
                Arg.Any<ProjectionRebuildStatus>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>(),
                Arg.Any<long?>())
            .Returns(ProjectionRebuildCheckpointSaveResult.Failure(StreamReplayReasonCodes.NoDomainService));
        AdminProjectionRebuildController controller = CreateController(store, asGlobalAdmin: true);

        IActionResult result = await controller.ReplayProjection(Tenant, Projection, new ProjectionReplayRequest(10, 20));

        ObjectResult problemResult = result.ShouldBeOfType<ObjectResult>();
        problemResult.StatusCode.ShouldBe(StatusCodes.Status503ServiceUnavailable);
        controller.Response.Headers.RetryAfter.ToString().ShouldBe("30");
    }

    [Fact]
    public async Task EraseProjectionWithGlobalAdministratorSuccessReturnsOkAndMapsOutcomes() {
        IProjectionRebuildCheckpointStore store = Substitute.For<IProjectionRebuildCheckpointStore>();
        IProjectionEraseCoordinator coordinator = Substitute.For<IProjectionEraseCoordinator>();
        _ = coordinator.EraseAsync(Arg.Any<ProjectionEraseRequest>(), Arg.Any<CancellationToken>())
            .Returns(ProjectionEraseResult.Of(
                ProjectionEraseOutcomeKind.Success,
                reasonCode: null,
                outcomes: [
                    new ProjectionEraseTargetOutcome("target-1", "Complete"),
                    new ProjectionEraseTargetOutcome("target-2", "Complete"),
                ]));
        AdminProjectionRebuildController controller = CreateController(store, asGlobalAdmin: true, eraseCoordinator: coordinator);

        IActionResult result = await controller.EraseProjectionAsync(
            Tenant,
            Projection,
            new ProjectionEraseRequestBody("party", "aggregate-1", ["slot-1", "slot-2"], "01HX0000000000000000000009"),
            CancellationToken.None);

        OkObjectResult ok = result.ShouldBeOfType<OkObjectResult>();
        ok.StatusCode.ShouldBe(StatusCodes.Status200OK);
        ProjectionEraseResponse response = ok.Value.ShouldBeOfType<ProjectionEraseResponse>();
        response.Outcome.ShouldBe(nameof(ProjectionEraseOutcomeKind.Success));
        response.Targets.Count.ShouldBe(2);
        response.Targets[0].TargetKey.ShouldBe("target-1");
        response.Targets[0].Outcome.ShouldBe("Complete");
        response.Targets[1].TargetKey.ShouldBe("target-2");
        _ = await coordinator.Received(1).EraseAsync(Arg.Any<ProjectionEraseRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EraseProjectionWithoutGlobalAdministratorRoleReturnsForbiddenBeforeCoordinator() {
        IProjectionRebuildCheckpointStore store = Substitute.For<IProjectionRebuildCheckpointStore>();
        IProjectionEraseCoordinator coordinator = Substitute.For<IProjectionEraseCoordinator>();
        AdminProjectionRebuildController controller = CreateController(store, asGlobalAdmin: false, eraseCoordinator: coordinator);

        IActionResult result = await controller.EraseProjectionAsync(
            Tenant,
            Projection,
            new ProjectionEraseRequestBody("party", "aggregate-1", ["slot-1"], "01HX0000000000000000000009"),
            CancellationToken.None);

        ObjectResult problemResult = result.ShouldBeOfType<ObjectResult>();
        problemResult.StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
        ProblemDetails problem = problemResult.Value.ShouldBeOfType<ProblemDetails>();
        problem.Type.ShouldBe(ProblemTypeUris.Forbidden);
        problem.Extensions["reasonCode"].ShouldBe(StreamReplayReasonCodes.ForbiddenRole);
        AssertNoForbiddenLeakage(problem);
        // Denial-before-resolution: no logical ID is ever resolved for an unauthorized caller.
        _ = await coordinator.DidNotReceiveWithAnyArgs().EraseAsync(default!, default);
    }

    [Fact]
    public async Task EraseProjectionUnsupportedReturnsBadRequest() {
        IProjectionRebuildCheckpointStore store = Substitute.For<IProjectionRebuildCheckpointStore>();
        IProjectionEraseCoordinator coordinator = Substitute.For<IProjectionEraseCoordinator>();
        _ = coordinator.EraseAsync(Arg.Any<ProjectionEraseRequest>(), Arg.Any<CancellationToken>())
            .Returns(ProjectionEraseResult.Of(ProjectionEraseOutcomeKind.Unsupported, "shared-slot"));
        AdminProjectionRebuildController controller = CreateController(store, asGlobalAdmin: true, eraseCoordinator: coordinator);

        IActionResult result = await controller.EraseProjectionAsync(
            Tenant,
            Projection,
            new ProjectionEraseRequestBody("party", "aggregate-1", ["slot-1"], "01HX0000000000000000000009"),
            CancellationToken.None);

        ObjectResult problemResult = result.ShouldBeOfType<ObjectResult>();
        problemResult.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        ProblemDetails problem = problemResult.Value.ShouldBeOfType<ProblemDetails>();
        problem.Extensions["reasonCode"].ShouldBe("shared-slot");
    }

    [Theory]
    [InlineData(ProjectionEraseOutcomeKind.ActiveRebuild)]
    [InlineData(ProjectionEraseOutcomeKind.Conflict)]
    [InlineData(ProjectionEraseOutcomeKind.Incomplete)]
    public async Task EraseProjectionRetryableOutcomesReturnConflictWithRetryAfterHeader(ProjectionEraseOutcomeKind kind) {
        IProjectionRebuildCheckpointStore store = Substitute.For<IProjectionRebuildCheckpointStore>();
        IProjectionEraseCoordinator coordinator = Substitute.For<IProjectionEraseCoordinator>();
        _ = coordinator.EraseAsync(Arg.Any<ProjectionEraseRequest>(), Arg.Any<CancellationToken>())
            .Returns(ProjectionEraseResult.Of(kind, "erase-conflict"));
        AdminProjectionRebuildController controller = CreateController(store, asGlobalAdmin: true, eraseCoordinator: coordinator);

        IActionResult result = await controller.EraseProjectionAsync(
            Tenant,
            Projection,
            new ProjectionEraseRequestBody("party", "aggregate-1", ["slot-1"], "01HX0000000000000000000009"),
            CancellationToken.None);

        ObjectResult problemResult = result.ShouldBeOfType<ObjectResult>();
        problemResult.StatusCode.ShouldBe(StatusCodes.Status409Conflict);
        controller.Response.Headers.RetryAfter.ToString().ShouldBe("5");
    }

    [Theory]
    [InlineData(ProjectionEraseOutcomeKind.Unknown)]
    [InlineData(ProjectionEraseOutcomeKind.Canceled)]
    public async Task EraseProjectionUnavailableOutcomesReturnServiceUnavailable(ProjectionEraseOutcomeKind kind) {
        IProjectionRebuildCheckpointStore store = Substitute.For<IProjectionRebuildCheckpointStore>();
        IProjectionEraseCoordinator coordinator = Substitute.For<IProjectionEraseCoordinator>();
        _ = coordinator.EraseAsync(Arg.Any<ProjectionEraseRequest>(), Arg.Any<CancellationToken>())
            .Returns(ProjectionEraseResult.Of(kind, "erase-unavailable"));
        AdminProjectionRebuildController controller = CreateController(store, asGlobalAdmin: true, eraseCoordinator: coordinator);

        IActionResult result = await controller.EraseProjectionAsync(
            Tenant,
            Projection,
            new ProjectionEraseRequestBody("party", "aggregate-1", ["slot-1"], "01HX0000000000000000000009"),
            CancellationToken.None);

        ObjectResult problemResult = result.ShouldBeOfType<ObjectResult>();
        problemResult.StatusCode.ShouldBe(StatusCodes.Status503ServiceUnavailable);
        ProblemDetails problem = problemResult.Value.ShouldBeOfType<ProblemDetails>();
        problem.Extensions["reasonCode"].ShouldBe("erase-unavailable");
    }

    [Fact]
    public async Task EraseProjectionDeniedOutcomeReturnsForbidden() {
        IProjectionRebuildCheckpointStore store = Substitute.For<IProjectionRebuildCheckpointStore>();
        IProjectionEraseCoordinator coordinator = Substitute.For<IProjectionEraseCoordinator>();
        _ = coordinator.EraseAsync(Arg.Any<ProjectionEraseRequest>(), Arg.Any<CancellationToken>())
            .Returns(ProjectionEraseResult.Of(ProjectionEraseOutcomeKind.Denied, "erase-denied"));
        AdminProjectionRebuildController controller = CreateController(store, asGlobalAdmin: true, eraseCoordinator: coordinator);

        IActionResult result = await controller.EraseProjectionAsync(
            Tenant,
            Projection,
            new ProjectionEraseRequestBody("party", "aggregate-1", ["slot-1"], "01HX0000000000000000000009"),
            CancellationToken.None);

        ObjectResult problemResult = result.ShouldBeOfType<ObjectResult>();
        problemResult.StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
        ProblemDetails problem = problemResult.Value.ShouldBeOfType<ProblemDetails>();
        problem.Extensions["reasonCode"].ShouldBe("erase-denied");
    }

    [Fact]
    public async Task EraseProjectionWithMissingBodyReturnsBadRequest() {
        IProjectionRebuildCheckpointStore store = Substitute.For<IProjectionRebuildCheckpointStore>();
        IProjectionEraseCoordinator coordinator = Substitute.For<IProjectionEraseCoordinator>();
        AdminProjectionRebuildController controller = CreateController(store, asGlobalAdmin: true, eraseCoordinator: coordinator);

        IActionResult result = await controller.EraseProjectionAsync(Tenant, Projection, body: null, CancellationToken.None);

        ObjectResult problemResult = result.ShouldBeOfType<ObjectResult>();
        problemResult.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        ProblemDetails problem = problemResult.Value.ShouldBeOfType<ProblemDetails>();
        problem.Extensions["reasonCode"].ShouldBe(StreamReplayReasonCodes.MissingRequiredField);
        _ = await coordinator.DidNotReceiveWithAnyArgs().EraseAsync(default!, default);
    }

    [Fact]
    public async Task EraseProjectionWithBlankSlotReturnsBadRequestBeforeCoordinator() {
        IProjectionRebuildCheckpointStore store = Substitute.For<IProjectionRebuildCheckpointStore>();
        IProjectionEraseCoordinator coordinator = Substitute.For<IProjectionEraseCoordinator>();
        AdminProjectionRebuildController controller = CreateController(store, asGlobalAdmin: true, eraseCoordinator: coordinator);

        IActionResult result = await controller.EraseProjectionAsync(
            Tenant,
            Projection,
            new ProjectionEraseRequestBody("party", "aggregate-1", ["   "], "01HX0000000000000000000009"),
            CancellationToken.None);

        ObjectResult problemResult = result.ShouldBeOfType<ObjectResult>();
        problemResult.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        _ = await coordinator.DidNotReceiveWithAnyArgs().EraseAsync(default!, default);
    }

    [Fact]
    public async Task EraseProjectionWithoutRegisteredCoordinatorReturnsServiceUnavailable() {
        IProjectionRebuildCheckpointStore store = Substitute.For<IProjectionRebuildCheckpointStore>();
        AdminProjectionRebuildController controller = CreateController(store, asGlobalAdmin: true);

        IActionResult result = await controller.EraseProjectionAsync(
            Tenant,
            Projection,
            new ProjectionEraseRequestBody("party", "aggregate-1", ["slot-1"], "01HX0000000000000000000009"),
            CancellationToken.None);

        ObjectResult problemResult = result.ShouldBeOfType<ObjectResult>();
        problemResult.StatusCode.ShouldBe(StatusCodes.Status503ServiceUnavailable);
    }

    [Fact]
    public async Task EraseProjectionPassesOnlyRouteAndBodyLogicalIdsIntoCoordinatorRequest() {
        IProjectionRebuildCheckpointStore store = Substitute.For<IProjectionRebuildCheckpointStore>();
        IProjectionEraseCoordinator coordinator = Substitute.For<IProjectionEraseCoordinator>();
        ProjectionEraseRequest? captured = null;
        _ = coordinator.EraseAsync(
                Arg.Do<ProjectionEraseRequest>(request => captured = request),
                Arg.Any<CancellationToken>())
            .Returns(ProjectionEraseResult.Of(ProjectionEraseOutcomeKind.Success));
        AdminProjectionRebuildController controller = CreateController(store, asGlobalAdmin: true, eraseCoordinator: coordinator);

        _ = await controller.EraseProjectionAsync(
            Tenant,
            Projection,
            new ProjectionEraseRequestBody("party", "aggregate-7", ["slot-a", "slot-b"], "01HX0000000000000000000009"),
            CancellationToken.None);

        ProjectionEraseRequest capturedRequest = captured.ShouldNotBeNull();
        capturedRequest.TenantId.ShouldBe(Tenant);
        capturedRequest.Domain.ShouldBe("party");
        capturedRequest.AggregateId.ShouldBe("aggregate-7");
        capturedRequest.ProjectionName.ShouldBe(Projection);
        capturedRequest.Slots.ShouldBe(["slot-a", "slot-b"]);
        capturedRequest.OperationId.ShouldBe("01HX0000000000000000000009");
    }

    [Fact]
    public void ProjectionEraseRequestBodyExposesOnlyLogicalIdMembers() {
        IEnumerable<string> memberNames = typeof(ProjectionEraseRequestBody)
            .GetProperties()
            .Select(property => property.Name);

        foreach (string name in memberNames) {
            foreach (string forbidden in new[] { "store", "key", "etag", "physical" }) {
                name.ShouldNotContain(forbidden, Case.Insensitive);
            }
        }
    }

    [Fact]
    public async Task ActivateDeliveryWriterProtocol_DeniesBeforeCutoverServiceMutation() {
        IProjectionRebuildCheckpointStore store = Substitute.For<IProjectionRebuildCheckpointStore>();
        IProjectionDeliveryCutover cutover = Substitute.For<IProjectionDeliveryCutover>();
        using ServiceProvider services = new ServiceCollection().AddSingleton(cutover).BuildServiceProvider();
        AdminProjectionRebuildController controller = CreateController(
            store,
            asGlobalAdmin: false,
            serviceProvider: services);

        IActionResult result = await controller.ActivateDeliveryWriterProtocolAsync(
            new ProjectionDeliveryCutoverRequestBody("commit", "backup", true, true, true),
            CancellationToken.None);

        result.ShouldBeOfType<ObjectResult>().StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
        _ = await cutover.DidNotReceiveWithAnyArgs().ActivateAsync(default!, default);
    }

    [Fact]
    public async Task ActivateDeliveryWriterProtocol_MapsSuccessfulAttestedCutover() {
        IProjectionRebuildCheckpointStore store = Substitute.For<IProjectionRebuildCheckpointStore>();
        IProjectionDeliveryCutover cutover = Substitute.For<IProjectionDeliveryCutover>();
        _ = cutover.ActivateAsync(Arg.Any<ProjectionDeliveryCutoverRequest>(), Arg.Any<CancellationToken>())
            .Returns(ProjectionDeliveryCutoverStatus.Activated);
        using ServiceProvider services = new ServiceCollection().AddSingleton(cutover).BuildServiceProvider();
        AdminProjectionRebuildController controller = CreateController(
            store,
            asGlobalAdmin: true,
            serviceProvider: services);

        IActionResult result = await controller.ActivateDeliveryWriterProtocolAsync(
            new ProjectionDeliveryCutoverRequestBody("commit-abc", "backup-7", true, true, true),
            CancellationToken.None);

        result.ShouldBeOfType<OkObjectResult>();
        _ = await cutover.Received(1).ActivateAsync(
            Arg.Is<ProjectionDeliveryCutoverRequest>(request =>
                request.CutoverCommit == "commit-abc"
                && request.BackupReference == "backup-7"
                && request.WritersQuiesced
                && request.RetryWorkersQuiesced
                && request.DowngradeProhibitedAcknowledged),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReconcileDelivery_AttributesJwtSubjectAndPassesExactScope() {
        IProjectionRebuildCheckpointStore store = Substitute.For<IProjectionRebuildCheckpointStore>();
        IProjectionDeliveryReconciler reconciler = Substitute.For<IProjectionDeliveryReconciler>();
        _ = reconciler.ReconcileFromEventStoreAsync(
                Arg.Any<AggregateIdentity>(),
                "order-detail",
                "operator-1",
                Arg.Any<CancellationToken>())
            .Returns(new ProjectionDeliveryReconciliationResult(
                ProjectionDeliveryReconciliationStatus.Completed,
                ProjectionDispatchReasonCodes.DeliveryReconciled,
                7));
        using ServiceProvider services = new ServiceCollection().AddSingleton(reconciler).BuildServiceProvider();
        AdminProjectionRebuildController controller = CreateController(
            store,
            asGlobalAdmin: true,
            serviceProvider: services);

        IActionResult result = await controller.ReconcileDeliveryFromEventStoreAsync(
            Tenant,
            "order-detail",
            "sales",
            "order-42",
            CancellationToken.None);

        result.ShouldBeOfType<OkObjectResult>();
        _ = await reconciler.Received(1).ReconcileFromEventStoreAsync(
            Arg.Is<AggregateIdentity>(identity =>
                identity.TenantId == Tenant
                && identity.Domain == "sales"
                && identity.AggregateId == "order-42"),
            "order-detail",
            "operator-1",
            Arg.Any<CancellationToken>());
    }

    private static AdminProjectionRebuildController CreateController(
        IProjectionRebuildCheckpointStore store,
        bool asGlobalAdmin,
        IProjectionRebuildOrchestrator? rebuildOrchestrator = null,
        IProjectionEraseCoordinator? eraseCoordinator = null,
        IServiceProvider? serviceProvider = null)
        => new(
            store,
            NullLogger<AdminProjectionRebuildController>.Instance,
            rebuildOrchestrator,
            eraseCoordinator,
            serviceProvider) {
            ControllerContext = new ControllerContext {
                HttpContext = new DefaultHttpContext {
                    User = CreatePrincipal(asGlobalAdmin),
                },
            },
        };

    private static ClaimsPrincipal CreatePrincipal(bool asGlobalAdmin) {
        List<Claim> claims = [new Claim("sub", "operator-1")];
        if (asGlobalAdmin) {
            claims.Add(new Claim(ClaimTypes.Role, "GlobalAdministrator"));
        }

        var identity = new ClaimsIdentity(claims, authenticationType: "test");
        return new ClaimsPrincipal(identity);
    }

    private static void AssertNoForbiddenLeakage(ProblemDetails problem) {
        string serialized = System.Text.Json.JsonSerializer.Serialize(problem);
        foreach (string forbidden in new[] {
            "state store",
            "statestore",
            "dapr://",
            "projection-rebuild-checkpoints:",
            "redis://",
            "localhost:",
            "127.0.0.1",
            "Bearer ",
            "stack trace",
            "at Hexalith.",
            "payload",
            "protected",
            "display name",
        }) {
            serialized.ShouldNotContain(forbidden, Case.Insensitive);
        }

        foreach (string forbiddenPattern in new[] { "\\bAggregateActor\\b", "\\bETag\\b" }) {
            System.Text.RegularExpressions.Regex.IsMatch(
                serialized,
                forbiddenPattern,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase).ShouldBeFalse();
        }
    }

    private static ProjectionRebuildCheckpoint CreateCheckpoint(
        long sequence,
        ProjectionRebuildStatus status,
        string operationId = "party-summary-rebuild",
        string? failureReasonCode = null,
        long? toPosition = null)
        => new(
            Tenant,
            Projection,
            Projection,
            null,
            operationId,
            sequence,
            status,
            DateTimeOffset.UtcNow,
            failureReasonCode,
            toPosition);
}
