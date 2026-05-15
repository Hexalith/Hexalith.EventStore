using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.Controllers;
using Hexalith.EventStore.Server.Projections;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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
        _ = store.SaveAsync(
                Arg.Any<ProjectionRebuildCheckpointScope>(),
                10,
                ProjectionRebuildStatus.Running,
                null,
                Arg.Any<CancellationToken>())
            .Returns(ProjectionRebuildCheckpointSaveResult.Success(CreateCheckpoint(10, ProjectionRebuildStatus.Running)));
        var controller = CreateController(store);

        IActionResult result = await controller.ReplayProjection(
            Tenant,
            Projection,
            new ProjectionReplayRequest(10, 20));

        AcceptedResult accepted = result.ShouldBeOfType<AcceptedResult>();
        accepted.StatusCode.ShouldBe(StatusCodes.Status202Accepted);
        AdminOperationResult operation = accepted.Value.ShouldBeOfType<AdminOperationResult>();
        operation.Success.ShouldBeTrue();
        operation.OperationId.ShouldBe("party-summary-rebuild");
        _ = await store.Received(1).SaveAsync(
            Arg.Is<ProjectionRebuildCheckpointScope>(scope =>
                scope.Tenant == Tenant
                && scope.Domain == Projection
                && scope.ProjectionName == Projection
                && scope.OperationId == "party-summary-rebuild"),
            10,
            ProjectionRebuildStatus.Running,
            null,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PauseProjectionIsIdempotentAndReturnsOkResult() {
        IProjectionRebuildCheckpointStore store = Substitute.For<IProjectionRebuildCheckpointStore>();
        _ = store.SaveAsync(
                Arg.Any<ProjectionRebuildCheckpointScope>(),
                0,
                ProjectionRebuildStatus.Paused,
                StreamReplayReasonCodes.RebuildPaused,
                Arg.Any<CancellationToken>())
            .Returns(ProjectionRebuildCheckpointSaveResult.Success(CreateCheckpoint(0, ProjectionRebuildStatus.Paused)));
        var controller = CreateController(store);

        IActionResult result = await controller.PauseProjection(Tenant, Projection);

        OkObjectResult ok = result.ShouldBeOfType<OkObjectResult>();
        AdminOperationResult operation = ok.Value.ShouldBeOfType<AdminOperationResult>();
        operation.Success.ShouldBeTrue();
        operation.ErrorCode.ShouldBeNull();
    }

    [Fact]
    public async Task ReplayProjectionCheckpointConflictReturnsProblemWithReasonCode() {
        IProjectionRebuildCheckpointStore store = Substitute.For<IProjectionRebuildCheckpointStore>();
        _ = store.SaveAsync(
                Arg.Any<ProjectionRebuildCheckpointScope>(),
                Arg.Any<long>(),
                Arg.Any<ProjectionRebuildStatus>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(ProjectionRebuildCheckpointSaveResult.Failure(StreamReplayReasonCodes.CheckpointConflict));
        var controller = CreateController(store);

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
        var controller = CreateController(store);

        IActionResult result = await controller.GetRebuildStatus(Tenant, Projection);

        OkObjectResult ok = result.ShouldBeOfType<OkObjectResult>();
        ProjectionRebuildOperation operation = ok.Value.ShouldBeOfType<ProjectionRebuildOperation>();
        operation.Status.ShouldBe(ProjectionRebuildStatus.NotStarted);
        operation.OperationId.ShouldBe("party-summary-rebuild");
    }

    private static AdminProjectionRebuildController CreateController(IProjectionRebuildCheckpointStore store)
        => new(store, NullLogger<AdminProjectionRebuildController>.Instance) {
            ControllerContext = new ControllerContext {
                HttpContext = new DefaultHttpContext(),
            },
        };

    private static ProjectionRebuildCheckpoint CreateCheckpoint(long sequence, ProjectionRebuildStatus status)
        => new(
            Tenant,
            Projection,
            Projection,
            null,
            "party-summary-rebuild",
            sequence,
            status,
            DateTimeOffset.UtcNow,
            status == ProjectionRebuildStatus.Paused ? StreamReplayReasonCodes.RebuildPaused : null);
}
