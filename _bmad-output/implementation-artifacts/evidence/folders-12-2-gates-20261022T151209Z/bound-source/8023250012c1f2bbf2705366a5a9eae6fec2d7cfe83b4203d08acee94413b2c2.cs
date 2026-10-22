using System.Diagnostics;
using System.Security.Claims;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Controllers;
using Hexalith.EventStore.Middleware;
using Hexalith.EventStore.Pipeline;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Pipeline.Commands;
using Hexalith.EventStore.Server.Telemetry;
using Hexalith.EventStore.Telemetry;

using MediatR;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Telemetry;

/// <summary>
/// Story 6.1 Task 9: EventStore tracing tests exercising real controller/pipeline execution paths.
/// </summary>
public class EventStoreTraceTests {
    [Fact]
    public async Task SubmitCommand_ThroughLoggingBehavior_CreatesSubmitActivityWithServerKindAsync() {
        // Arrange
        string correlationId = $"submit-{Guid.NewGuid()}";
        Activity? capturedActivity = null;

        using ActivityListener listener = CreateEventStoreListener((activity) => {
            if (activity.OperationName == EventStoreActivitySources.Submit
                && Equals(activity.GetTagItem(EventStoreActivitySource.TagCorrelationId), correlationId)) {
                capturedActivity = activity;
            }
        });

        ILogger<LoggingBehavior<SubmitCommand, SubmitCommandResult>> logger =
            Substitute.For<ILogger<LoggingBehavior<SubmitCommand, SubmitCommandResult>>>();
        IHttpContextAccessor accessor = Substitute.For<IHttpContextAccessor>();
        var httpContext = new DefaultHttpContext();
        httpContext.Items[CorrelationIdMiddleware.HttpContextKey] = correlationId;
        _ = accessor.HttpContext.Returns(httpContext);

        var behavior = new LoggingBehavior<SubmitCommand, SubmitCommandResult>(logger, accessor);
        var command = new SubmitCommand(
            MessageId: "msg-1",
            Tenant: "tenant-a",
            Domain: "orders",
            AggregateId: "agg-001",
            CommandType: "CreateOrder",
            Payload: [1],
            CorrelationId: correlationId,
            UserId: "user-1",
            Extensions: null);

        // Act
        _ = await behavior.Handle(
            command,
            (_) => Task.FromResult(new SubmitCommandResult(correlationId)),
            CancellationToken.None);

        // Assert
        _ = capturedActivity.ShouldNotBeNull();
        capturedActivity.Kind.ShouldBe(ActivityKind.Server);
        capturedActivity.Status.ShouldBe(ActivityStatusCode.Ok);
    }

    [Fact]
    public async Task QueryStatus_Controller_CreatesQueryStatusActivity_OnSuccessAsync() {
        // Arrange
        string correlationId = Guid.NewGuid().ToString();
        Activity? capturedActivity = null;

        using ActivityListener listener = CreateEventStoreListener((activity) => {
            if (activity.OperationName == EventStoreActivitySources.QueryStatus
                && Equals(activity.GetTagItem(EventStoreActivitySource.TagCorrelationId), correlationId)) {
                capturedActivity = activity;
            }
        });

        ICommandStatusStore statusStore = Substitute.For<ICommandStatusStore>();
        _ = statusStore.ReadStatusAsync("tenant-a", correlationId, Arg.Any<CancellationToken>())
            .Returns(new CommandStatusRecord(
                CommandStatus.Completed,
                DateTimeOffset.UtcNow,
                "agg-001",
                EventCount: 1,
                RejectionEventType: null,
                FailureReason: null,
                TimeoutDuration: null));

        ILogger<CommandStatusController> logger = Substitute.For<ILogger<CommandStatusController>>();
        var controller = new CommandStatusController(statusStore, logger) {
            ControllerContext = new ControllerContext {
                HttpContext = CreateHttpContext(
                    correlationId,
                    ["tenant-a"]),
            },
        };

        // Act
        IActionResult action = await controller.GetStatus(correlationId, CancellationToken.None);

        // Assert
        _ = action.ShouldBeOfType<OkObjectResult>();
        _ = capturedActivity.ShouldNotBeNull();
        capturedActivity.Kind.ShouldBe(ActivityKind.Server);
        capturedActivity.Status.ShouldBe(ActivityStatusCode.Ok);
        capturedActivity.GetTagItem(EventStoreActivitySource.TagTenantId).ShouldBe("tenant-a");
    }

    [Fact]
    public async Task QueryStatus_Controller_SetsErrorStatus_OnNotFoundAsync() {
        // Arrange
        string correlationId = Guid.NewGuid().ToString();
        Activity? capturedActivity = null;

        using ActivityListener listener = CreateEventStoreListener((activity) => {
            if (activity.OperationName == EventStoreActivitySources.QueryStatus
                && Equals(activity.GetTagItem(EventStoreActivitySource.TagCorrelationId), correlationId)) {
                capturedActivity = activity;
            }
        });

        ICommandStatusStore statusStore = Substitute.For<ICommandStatusStore>();
        _ = statusStore.ReadStatusAsync("tenant-a", correlationId, Arg.Any<CancellationToken>())
            .Returns((CommandStatusRecord?)null);

        ILogger<CommandStatusController> logger = Substitute.For<ILogger<CommandStatusController>>();
        var controller = new CommandStatusController(statusStore, logger) {
            ControllerContext = new ControllerContext {
                HttpContext = CreateHttpContext(
                    correlationId,
                    ["tenant-a"]),
            },
        };

        // Act
        IActionResult action = await controller.GetStatus(correlationId, CancellationToken.None);

        // Assert
        ObjectResult result = action.ShouldBeOfType<ObjectResult>();
        result.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
        _ = capturedActivity.ShouldNotBeNull();
        capturedActivity.Status.ShouldBe(ActivityStatusCode.Error);
    }

    [Fact]
    public async Task Replay_Controller_CreatesReplayActivity_OnSuccessAsync() {
        // Arrange
        string correlationId = Guid.NewGuid().ToString();
        Activity? capturedActivity = null;

        using ActivityListener listener = CreateEventStoreListener((activity) => {
            if (activity.OperationName == EventStoreActivitySources.Replay) {
                capturedActivity = activity;
            }
        });

        ICommandArchiveStore archiveStore = Substitute.For<ICommandArchiveStore>();
        _ = archiveStore.ReadCommandAsync("tenant-a", correlationId, Arg.Any<CancellationToken>())
            .Returns(new ArchivedCommand(
                Tenant: "tenant-a",
                Domain: "orders",
                AggregateId: "agg-001",
                CommandType: "CreateOrder",
                Payload: [1],
                Extensions: null,
                OriginalTimestamp: DateTimeOffset.UtcNow));

        ICommandStatusStore statusStore = Substitute.For<ICommandStatusStore>();
        _ = statusStore.ReadStatusAsync("tenant-a", correlationId, Arg.Any<CancellationToken>())
            .Returns(new CommandStatusRecord(
                CommandStatus.Rejected,
                DateTimeOffset.UtcNow,
                "agg-001",
                EventCount: null,
                RejectionEventType: "OrderRejected",
                FailureReason: null,
                TimeoutDuration: null));

        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => new SubmitCommandResult(callInfo.Arg<SubmitCommand>().CorrelationId));

        ILogger<ReplayController> logger = Substitute.For<ILogger<ReplayController>>();
        var controller = new ReplayController(archiveStore, statusStore, mediator, logger) {
            ControllerContext = new ControllerContext {
                HttpContext = CreateHttpContext(
                    correlationId,
                    ["tenant-a"]),
            },
        };

        // Act
        IActionResult action = await controller.Replay(correlationId, CancellationToken.None);

        // Assert — replay generates a new correlation ID, so result type is AcceptedResult
        AcceptedResult acceptedResult = action.ShouldBeOfType<AcceptedResult>();
        _ = capturedActivity.ShouldNotBeNull();
        capturedActivity.Kind.ShouldBe(ActivityKind.Server);
        capturedActivity.Status.ShouldBe(ActivityStatusCode.Ok);
        capturedActivity.GetTagItem(EventStoreActivitySource.TagTenantId).ShouldBe("tenant-a");
    }

    [Fact]
    public async Task Replay_Controller_SetsErrorStatus_OnConflictAsync() {
        // Arrange
        string correlationId = Guid.NewGuid().ToString();
        Activity? capturedActivity = null;

        using ActivityListener listener = CreateEventStoreListener((activity) => {
            if (activity.OperationName == EventStoreActivitySources.Replay) {
                capturedActivity = activity;
            }
        });

        ICommandArchiveStore archiveStore = Substitute.For<ICommandArchiveStore>();
        _ = archiveStore.ReadCommandAsync("tenant-a", correlationId, Arg.Any<CancellationToken>())
            .Returns(new ArchivedCommand(
                Tenant: "tenant-a",
                Domain: "orders",
                AggregateId: "agg-001",
                CommandType: "CreateOrder",
                Payload: [1],
                Extensions: null,
                OriginalTimestamp: DateTimeOffset.UtcNow));

        ICommandStatusStore statusStore = Substitute.For<ICommandStatusStore>();
        _ = statusStore.ReadStatusAsync("tenant-a", correlationId, Arg.Any<CancellationToken>())
            .Returns(new CommandStatusRecord(
                CommandStatus.Completed,
                DateTimeOffset.UtcNow,
                "agg-001",
                EventCount: 1,
                RejectionEventType: null,
                FailureReason: null,
                TimeoutDuration: null));

        IMediator mediator = Substitute.For<IMediator>();
        ILogger<ReplayController> logger = Substitute.For<ILogger<ReplayController>>();

        var controller = new ReplayController(archiveStore, statusStore, mediator, logger) {
            ControllerContext = new ControllerContext {
                HttpContext = CreateHttpContext(
                    correlationId,
                    ["tenant-a"]),
            },
        };

        // Act
        IActionResult action = await controller.Replay(correlationId, CancellationToken.None);

        // Assert
        ObjectResult result = action.ShouldBeOfType<ObjectResult>();
        result.StatusCode.ShouldBe(StatusCodes.Status409Conflict);
        _ = capturedActivity.ShouldNotBeNull();
        capturedActivity.Status.ShouldBe(ActivityStatusCode.Error);
    }

    private static ActivityListener CreateEventStoreListener(Action<Activity> onActivityStopped) {
        var listener = new ActivityListener {
            ShouldListenTo = source => source.Name == "Hexalith.EventStore",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = onActivityStopped,
        };

        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    private static DefaultHttpContext CreateHttpContext(string requestCorrelationId, IReadOnlyCollection<string> tenantClaims) {
        var context = new DefaultHttpContext();
        context.Items[CorrelationIdMiddleware.HttpContextKey] = requestCorrelationId;
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("localhost");

        var claims = new List<Claim>();
        foreach (string tenant in tenantClaims) {
            claims.Add(new Claim("eventstore:tenant", tenant));
        }

        claims.Add(new Claim("sub", "test-user"));

        var identity = new ClaimsIdentity(claims, authenticationType: "TestAuth");
        context.User = new ClaimsPrincipal(identity);

        return context;
    }
}
