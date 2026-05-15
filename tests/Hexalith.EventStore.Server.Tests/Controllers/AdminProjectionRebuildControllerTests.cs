using System.Security.Claims;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.Controllers;
using Hexalith.EventStore.ErrorHandling;
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
        _ = store.ResetAsync(
                Arg.Any<ProjectionRebuildCheckpointScope>(),
                9,
                ProjectionRebuildStatus.Running,
                null,
                Arg.Any<CancellationToken>(),
                20)
            .Returns(ProjectionRebuildCheckpointSaveResult.Success(CreateCheckpoint(9, ProjectionRebuildStatus.Running, "01HX0000000000000000000000", 20)));
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
    public async Task PauseProjectionTransitionsExistingRebuildAndReturnsOkResult() {
        IProjectionRebuildCheckpointStore store = Substitute.For<IProjectionRebuildCheckpointStore>();
        ProjectionRebuildCheckpoint existing = CreateCheckpoint(50, ProjectionRebuildStatus.Running);
        _ = store.ReadAsync(Arg.Any<ProjectionRebuildCheckpointScope>(), Arg.Any<CancellationToken>())
            .Returns(existing);
        _ = store.SaveAsync(
                Arg.Any<ProjectionRebuildCheckpointScope>(),
                50,
                ProjectionRebuildStatus.Paused,
                null,
                Arg.Any<CancellationToken>())
            .Returns(ProjectionRebuildCheckpointSaveResult.Success(CreateCheckpoint(50, ProjectionRebuildStatus.Paused)));
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
            Arg.Any<CancellationToken>());
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
        _ = await store.DidNotReceiveWithAnyArgs().ReadAsync(default!, default);
        _ = await store.DidNotReceiveWithAnyArgs().SaveAsync(default!, default, default, default, default);
    }

    private static AdminProjectionRebuildController CreateController(IProjectionRebuildCheckpointStore store, bool asGlobalAdmin)
        => new(store, NullLogger<AdminProjectionRebuildController>.Instance) {
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

    private static ProjectionRebuildCheckpoint CreateCheckpoint(
        long sequence,
        ProjectionRebuildStatus status,
        string operationId = "party-summary-rebuild",
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
            null,
            toPosition);
}
