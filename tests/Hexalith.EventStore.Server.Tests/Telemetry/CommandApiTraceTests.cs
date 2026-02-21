using System.Diagnostics;
using System.Security.Claims;

using Hexalith.EventStore.CommandApi.Controllers;
using Hexalith.EventStore.CommandApi.Middleware;
using Hexalith.EventStore.CommandApi.Pipeline;
using Hexalith.EventStore.CommandApi.Telemetry;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Pipeline.Commands;
using Hexalith.EventStore.Server.Telemetry;

using MediatR;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Telemetry;

/// <summary>
/// Story 6.1 Task 9: CommandApi tracing tests exercising real controller/pipeline execution paths.
/// </summary>
public class CommandApiTraceTests
{
    [Fact]
    public async Task SubmitCommand_ThroughLoggingBehavior_CreatesSubmitActivityWithServerKindAsync()
    {
        // Arrange
        string correlationId = $"submit-{Guid.NewGuid()}";
        Activity? capturedActivity = null;

        using var listener = CreateCommandApiListener((activity) =>
        {
            if (activity.OperationName == EventStoreActivitySources.Submit
                && Equals(activity.GetTagItem(EventStoreActivitySource.TagCorrelationId), correlationId))
            {
                capturedActivity = activity;
            }
        });

        ILogger<LoggingBehavior<SubmitCommand, SubmitCommandResult>> logger =
            Substitute.For<ILogger<LoggingBehavior<SubmitCommand, SubmitCommandResult>>>();
        IHttpContextAccessor accessor = Substitute.For<IHttpContextAccessor>();
        var httpContext = new DefaultHttpContext();
        httpContext.Items[CorrelationIdMiddleware.HttpContextKey] = correlationId;
        accessor.HttpContext.Returns(httpContext);

        var behavior = new LoggingBehavior<SubmitCommand, SubmitCommandResult>(logger, accessor);
        var command = new SubmitCommand(
            Tenant: "tenant-a",
            Domain: "orders",
            AggregateId: "agg-001",
            CommandType: "CreateOrder",
            Payload: [1],
            CorrelationId: correlationId,
            UserId: "user-1",
            Extensions: null);

        // Act
        await behavior.Handle(
            command,
            (_) => Task.FromResult(new SubmitCommandResult(correlationId)),
            CancellationToken.None);

        // Assert
        capturedActivity.ShouldNotBeNull();
        capturedActivity.Kind.ShouldBe(ActivityKind.Server);
        capturedActivity.Status.ShouldBe(ActivityStatusCode.Ok);
    }

    [Fact]
    public async Task QueryStatus_Controller_CreatesQueryStatusActivity_OnSuccessAsync()
    {
        // Arrange
        string correlationId = Guid.NewGuid().ToString();
        Activity? capturedActivity = null;

        using var listener = CreateCommandApiListener((activity) =>
        {
            if (activity.OperationName == EventStoreActivitySources.QueryStatus
                && Equals(activity.GetTagItem(EventStoreActivitySource.TagCorrelationId), correlationId))
            {
                capturedActivity = activity;
            }
        });

        ICommandStatusStore statusStore = Substitute.For<ICommandStatusStore>();
        statusStore.ReadStatusAsync("tenant-a", correlationId, Arg.Any<CancellationToken>())
            .Returns(new CommandStatusRecord(
                CommandStatus.Completed,
                DateTimeOffset.UtcNow,
                "agg-001",
                EventCount: 1,
                RejectionEventType: null,
                FailureReason: null,
                TimeoutDuration: null));

        var logger = Substitute.For<ILogger<CommandStatusController>>();
        var controller = new CommandStatusController(statusStore, logger)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = CreateHttpContext(
                    correlationId,
                    ["tenant-a"]),
            },
        };

        // Act
        IActionResult action = await controller.GetStatus(correlationId, CancellationToken.None);

        // Assert
        action.ShouldBeOfType<OkObjectResult>();
        capturedActivity.ShouldNotBeNull();
        capturedActivity.Kind.ShouldBe(ActivityKind.Server);
        capturedActivity.Status.ShouldBe(ActivityStatusCode.Ok);
        capturedActivity.GetTagItem(EventStoreActivitySource.TagTenantId).ShouldBe("tenant-a");
    }

    [Fact]
    public async Task QueryStatus_Controller_SetsErrorStatus_OnNotFoundAsync()
    {
        // Arrange
        string correlationId = Guid.NewGuid().ToString();
        Activity? capturedActivity = null;

        using var listener = CreateCommandApiListener((activity) =>
        {
            if (activity.OperationName == EventStoreActivitySources.QueryStatus
                && Equals(activity.GetTagItem(EventStoreActivitySource.TagCorrelationId), correlationId))
            {
                capturedActivity = activity;
            }
        });

        ICommandStatusStore statusStore = Substitute.For<ICommandStatusStore>();
        statusStore.ReadStatusAsync("tenant-a", correlationId, Arg.Any<CancellationToken>())
            .Returns((CommandStatusRecord?)null);

        var logger = Substitute.For<ILogger<CommandStatusController>>();
        var controller = new CommandStatusController(statusStore, logger)
        {
            ControllerContext = new ControllerContext
            {
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
        capturedActivity.ShouldNotBeNull();
        capturedActivity.Status.ShouldBe(ActivityStatusCode.Error);
    }

    [Fact]
    public async Task Replay_Controller_CreatesReplayActivity_OnSuccessAsync()
    {
        // Arrange
        string correlationId = $"replay-{Guid.NewGuid()}";
        Activity? capturedActivity = null;

        using var listener = CreateCommandApiListener((activity) =>
        {
            if (activity.OperationName == EventStoreActivitySources.Replay
                && Equals(activity.GetTagItem(EventStoreActivitySource.TagCorrelationId), correlationId))
            {
                capturedActivity = activity;
            }
        });

        ICommandArchiveStore archiveStore = Substitute.For<ICommandArchiveStore>();
        archiveStore.ReadCommandAsync("tenant-a", correlationId, Arg.Any<CancellationToken>())
            .Returns(new ArchivedCommand(
                Tenant: "tenant-a",
                Domain: "orders",
                AggregateId: "agg-001",
                CommandType: "CreateOrder",
                Payload: [1],
                Extensions: null,
                OriginalTimestamp: DateTimeOffset.UtcNow));

        ICommandStatusStore statusStore = Substitute.For<ICommandStatusStore>();
        statusStore.ReadStatusAsync("tenant-a", correlationId, Arg.Any<CancellationToken>())
            .Returns(new CommandStatusRecord(
                CommandStatus.Rejected,
                DateTimeOffset.UtcNow,
                "agg-001",
                EventCount: null,
                RejectionEventType: "OrderRejected",
                FailureReason: null,
                TimeoutDuration: null));

        IMediator mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>())
            .Returns(new SubmitCommandResult(correlationId));

        var logger = Substitute.For<ILogger<ReplayController>>();
        var controller = new ReplayController(archiveStore, statusStore, mediator, logger)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = CreateHttpContext(
                    correlationId,
                    ["tenant-a"]),
            },
        };

        // Act
        IActionResult action = await controller.Replay(correlationId, CancellationToken.None);

        // Assert
        action.ShouldBeOfType<AcceptedResult>();
        capturedActivity.ShouldNotBeNull();
        capturedActivity.Kind.ShouldBe(ActivityKind.Server);
        capturedActivity.Status.ShouldBe(ActivityStatusCode.Ok);
        capturedActivity.GetTagItem(EventStoreActivitySource.TagTenantId).ShouldBe("tenant-a");
    }

    [Fact]
    public async Task Replay_Controller_SetsErrorStatus_OnConflictAsync()
    {
        // Arrange
        string correlationId = $"conflict-{Guid.NewGuid()}";
        Activity? capturedActivity = null;

        using var listener = CreateCommandApiListener((activity) =>
        {
            if (activity.OperationName == EventStoreActivitySources.Replay
                && Equals(activity.GetTagItem(EventStoreActivitySource.TagCorrelationId), correlationId))
            {
                capturedActivity = activity;
            }
        });

        ICommandArchiveStore archiveStore = Substitute.For<ICommandArchiveStore>();
        archiveStore.ReadCommandAsync("tenant-a", correlationId, Arg.Any<CancellationToken>())
            .Returns(new ArchivedCommand(
                Tenant: "tenant-a",
                Domain: "orders",
                AggregateId: "agg-001",
                CommandType: "CreateOrder",
                Payload: [1],
                Extensions: null,
                OriginalTimestamp: DateTimeOffset.UtcNow));

        ICommandStatusStore statusStore = Substitute.For<ICommandStatusStore>();
        statusStore.ReadStatusAsync("tenant-a", correlationId, Arg.Any<CancellationToken>())
            .Returns(new CommandStatusRecord(
                CommandStatus.Completed,
                DateTimeOffset.UtcNow,
                "agg-001",
                EventCount: 1,
                RejectionEventType: null,
                FailureReason: null,
                TimeoutDuration: null));

        IMediator mediator = Substitute.For<IMediator>();
        var logger = Substitute.For<ILogger<ReplayController>>();

        var controller = new ReplayController(archiveStore, statusStore, mediator, logger)
        {
            ControllerContext = new ControllerContext
            {
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
        capturedActivity.ShouldNotBeNull();
        capturedActivity.Status.ShouldBe(ActivityStatusCode.Error);
    }

    private static ActivityListener CreateCommandApiListener(Action<Activity> onActivityStopped)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Hexalith.EventStore.CommandApi",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = onActivityStopped,
        };

        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    private static DefaultHttpContext CreateHttpContext(string requestCorrelationId, IReadOnlyCollection<string> tenantClaims)
    {
        var context = new DefaultHttpContext();
        context.Items[CorrelationIdMiddleware.HttpContextKey] = requestCorrelationId;
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("localhost");

        var claims = new List<Claim>();
        foreach (string tenant in tenantClaims)
        {
            claims.Add(new Claim("eventstore:tenant", tenant));
        }

        claims.Add(new Claim("sub", "test-user"));

        var identity = new ClaimsIdentity(claims, authenticationType: "TestAuth");
        context.User = new ClaimsPrincipal(identity);

        return context;
    }
}
