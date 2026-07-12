
using System.Security.Claims;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Controllers;
using Hexalith.EventStore.Models;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Testing.Fakes;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Commands;

public class CommandStatusControllerTests {
    private readonly InMemoryCommandStatusStore _statusStore = new();
    private readonly CommandStatusController _controller;
    private const string CorrelationId = "01ARZ3NDEKTSV4RRFFQ69G5FAV";

    public CommandStatusControllerTests() {
        _controller = new CommandStatusController(_statusStore, NullLogger<CommandStatusController>.Instance);
        SetupHttpContext("default-tenant");
    }

    [Fact]
    public async Task GetStatus_ExistingStatus_Returns200WithRecord() {
        // Arrange
        string correlationId = CorrelationId;
        SetupHttpContext("tenant-a");
        await _statusStore.WriteStatusAsync(
            "tenant-a", correlationId,
            new CommandStatusRecord(CommandStatus.Received, DateTimeOffset.UtcNow, "agg-1", null, null, null, null),
            CancellationToken.None);

        // Act
        IActionResult result = await _controller.GetStatus(correlationId, CancellationToken.None);

        // Assert
        OkObjectResult okResult = result.ShouldBeOfType<OkObjectResult>();
        CommandStatusResponse response = okResult.Value.ShouldBeOfType<CommandStatusResponse>();
        response.Status.ShouldBe("Received");
        response.AggregateId.ShouldBe("agg-1");
        response.MessageId.ShouldBeNull("legacy correlation-primary records must not infer command identity");
    }

    [Fact]
    public async Task GetStatus_MessagePrimary_ReturnsDistinctMessageAndCorrelationIdentity() {
        const string messageId = "01MESSAGEPRIMARY000000000001";
        const string correlationId = "01CORRELATIONTRACE0000000001";
        var store = new InMemoryCommandStatusStore();
        await store.WriteStatusAsync(
            "tenant-a",
            messageId,
            new CommandStatusRecord(
                CommandStatus.Completed,
                DateTimeOffset.UtcNow,
                "agg-1",
                1,
                null,
                null,
                null,
                messageId,
                correlationId),
            CancellationToken.None);
        var controller = CreateController(store, new InMemoryCommandCorrelationIndex(), "tenant-a");

        IActionResult result = await controller.GetStatus(messageId, CancellationToken.None);

        CommandStatusResponse response = result.ShouldBeOfType<OkObjectResult>()
            .Value.ShouldBeOfType<CommandStatusResponse>();
        response.MessageId.ShouldBe(messageId);
        response.CorrelationId.ShouldBe(correlationId);
    }

    [Fact]
    public async Task GetStatus_UniqueCorrelationIndexResolution_ReadsMessagePrimaryRecord() {
        const string messageId = "01MESSAGEPRIMARY000000000002";
        const string correlationId = "01CORRELATIONTRACE0000000002";
        var store = new InMemoryCommandStatusStore();
        var index = new InMemoryCommandCorrelationIndex();
        await store.WriteStatusAsync(
            "tenant-a",
            messageId,
            new CommandStatusRecord(CommandStatus.Completed, DateTimeOffset.UtcNow, "agg-2", 0, null, null, null, messageId, correlationId),
            CancellationToken.None);
        _ = await index.AddAsync("tenant-a", correlationId, messageId);
        var controller = CreateController(store, index, "tenant-a");

        IActionResult result = await controller.GetStatus(correlationId, CancellationToken.None);

        CommandStatusResponse response = result.ShouldBeOfType<OkObjectResult>()
            .Value.ShouldBeOfType<CommandStatusResponse>();
        response.MessageId.ShouldBe(messageId);
        response.CorrelationId.ShouldBe(correlationId);
    }

    [Fact]
    public async Task GetStatus_AmbiguousCorrelation_ReturnsSupportSafe409() {
        const string correlationId = "01AMBIGUOUSCORRELATION000001";
        var index = new InMemoryCommandCorrelationIndex();
        _ = await index.AddAsync("tenant-a", correlationId, "01MESSAGE000000000000000001");
        _ = await index.AddAsync("tenant-a", correlationId, "01MESSAGE000000000000000002");
        var controller = CreateController(new InMemoryCommandStatusStore(), index, "tenant-a");

        IActionResult result = await controller.GetStatus(correlationId, CancellationToken.None);

        ObjectResult conflict = result.ShouldBeOfType<ObjectResult>();
        conflict.StatusCode.ShouldBe(StatusCodes.Status409Conflict);
        ProblemDetails problem = conflict.Value.ShouldBeOfType<ProblemDetails>();
        problem.Type.ShouldBe("https://hexalith.io/problems/command-correlation-ambiguous");
        problem.Detail.ShouldContain("MessageId");
        problem.Detail.ShouldNotContain("01MESSAGE000000000000000001");
    }

    [Fact]
    public async Task GetStatus_AuthorizedTenantOnly_DoesNotProbeOtherTenantStatusOrIndex() {
        ICommandStatusStore store = Substitute.For<ICommandStatusStore>();
        ICommandCorrelationIndex index = Substitute.For<ICommandCorrelationIndex>();
        _ = index.ResolveAsync("tenant-a", CorrelationId, Arg.Any<CancellationToken>())
            .Returns(new CommandCorrelationResolution(CommandCorrelationResolutionOutcome.NotFound));
        var controller = CreateController(store, index, "tenant-a");

        _ = await controller.GetStatus(CorrelationId, CancellationToken.None);

        _ = await store.Received(1).ReadStatusAsync("tenant-a", CorrelationId, Arg.Any<CancellationToken>());
        _ = await index.Received(1).ResolveAsync("tenant-a", CorrelationId, Arg.Any<CancellationToken>());
        _ = await store.DidNotReceive().ReadStatusAsync("tenant-secret", Arg.Any<string>(), Arg.Any<CancellationToken>());
        _ = await index.DidNotReceive().ResolveAsync("tenant-secret", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetStatus_NonExistentCorrelationId_Returns404ProblemDetails() {
        // Arrange
        string correlationId = CorrelationId;
        SetupHttpContext("tenant-a");

        // Act
        IActionResult result = await _controller.GetStatus(correlationId, CancellationToken.None);

        // Assert
        ObjectResult objResult = result.ShouldBeOfType<ObjectResult>();
        objResult.StatusCode.ShouldBe(404);
        ProblemDetails pd = objResult.Value.ShouldBeOfType<ProblemDetails>();
        pd.Detail!.ShouldContain(correlationId);
        pd.Type.ShouldBe("https://hexalith.io/problems/command-status-not-found");
        pd.Title.ShouldBe("Not Found");
        pd.Extensions.ShouldContainKey("correlationId");
        _controller.HttpContext.Response.ContentType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task GetStatus_TenantMismatch_Returns404ProblemDetails() {
        // Arrange - status belongs to tenant-a
        string correlationId = CorrelationId;
        await _statusStore.WriteStatusAsync(
            "tenant-a", correlationId,
            new CommandStatusRecord(CommandStatus.Received, DateTimeOffset.UtcNow, "agg-1", null, null, null, null),
            CancellationToken.None);

        // Query as tenant-b (SEC-3: should return 404 not 403)
        SetupHttpContext("tenant-b");

        // Act
        IActionResult result = await _controller.GetStatus(correlationId, CancellationToken.None);

        // Assert
        ObjectResult objResult = result.ShouldBeOfType<ObjectResult>();
        objResult.StatusCode.ShouldBe(404);
    }

    [Fact]
    public async Task GetStatus_NoTenantClaims_Returns403ProblemDetails() {
        // Arrange - user with no tenant claims
        string correlationId = CorrelationId;
        SetupHttpContextWithClaims([]); // no tenant claims

        // Act
        IActionResult result = await _controller.GetStatus(correlationId, CancellationToken.None);

        // Assert
        ObjectResult objResult = result.ShouldBeOfType<ObjectResult>();
        objResult.StatusCode.ShouldBe(403);
        ProblemDetails pd = objResult.Value.ShouldBeOfType<ProblemDetails>();
        pd.Detail!.ShouldContain("No tenant authorization claims found");
        pd.Type.ShouldBe("https://hexalith.io/problems/forbidden");
        pd.Extensions.ShouldContainKey("correlationId");
        _controller.HttpContext.Response.ContentType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task GetStatus_MultipleTenantClaims_TriesAllTenants() {
        // Arrange - status belongs to tenant-b, user has [tenant-a, tenant-b]
        string correlationId = CorrelationId;
        await _statusStore.WriteStatusAsync(
            "tenant-b", correlationId,
            new CommandStatusRecord(CommandStatus.Processing, DateTimeOffset.UtcNow, "agg-multi", null, null, null, null),
            CancellationToken.None);

        SetupHttpContextWithClaims(["tenant-a", "tenant-b"]);

        // Act
        IActionResult result = await _controller.GetStatus(correlationId, CancellationToken.None);

        // Assert
        OkObjectResult okResult = result.ShouldBeOfType<OkObjectResult>();
        CommandStatusResponse response = okResult.Value.ShouldBeOfType<CommandStatusResponse>();
        response.Status.ShouldBe("Processing");
    }

    [Fact]
    public async Task GetStatus_CompletedStatus_IncludesEventCount() {
        // Arrange
        string correlationId = CorrelationId;
        SetupHttpContext("tenant-a");
        await _statusStore.WriteStatusAsync(
            "tenant-a", correlationId,
            new CommandStatusRecord(CommandStatus.Completed, DateTimeOffset.UtcNow, "agg-done", 5, null, null, null),
            CancellationToken.None);

        // Act
        IActionResult result = await _controller.GetStatus(correlationId, CancellationToken.None);

        // Assert
        OkObjectResult okResult = result.ShouldBeOfType<OkObjectResult>();
        CommandStatusResponse response = okResult.Value.ShouldBeOfType<CommandStatusResponse>();
        response.Status.ShouldBe("Completed");
        response.EventCount.ShouldBe(5);
    }

    [Fact]
    public async Task GetStatus_RejectedStatus_IncludesRejectionEventType() {
        // Arrange
        string correlationId = CorrelationId;
        SetupHttpContext("tenant-a");
        await _statusStore.WriteStatusAsync(
            "tenant-a", correlationId,
            new CommandStatusRecord(CommandStatus.Rejected, DateTimeOffset.UtcNow, "agg-rej", null, "OrderRejected", null, null),
            CancellationToken.None);

        // Act
        IActionResult result = await _controller.GetStatus(correlationId, CancellationToken.None);

        // Assert
        OkObjectResult okResult = result.ShouldBeOfType<OkObjectResult>();
        CommandStatusResponse response = okResult.Value.ShouldBeOfType<CommandStatusResponse>();
        response.Status.ShouldBe("Rejected");
        response.RejectionEventType.ShouldBe("OrderRejected");
    }

    [Fact]
    public async Task GetStatus_RejectedInfrastructureFailure_IncludesFailureReason() {
        // Arrange
        string correlationId = CorrelationId;
        SetupHttpContext("tenant-a");
        await _statusStore.WriteStatusAsync(
            "tenant-a", correlationId,
            new CommandStatusRecord(CommandStatus.Rejected, DateTimeOffset.UtcNow, "agg-rej", null, null, "Service unavailable", null),
            CancellationToken.None);

        // Act
        IActionResult result = await _controller.GetStatus(correlationId, CancellationToken.None);

        // Assert
        OkObjectResult okResult = result.ShouldBeOfType<OkObjectResult>();
        CommandStatusResponse response = okResult.Value.ShouldBeOfType<CommandStatusResponse>();
        response.Status.ShouldBe("Rejected");
        response.FailureReason.ShouldBe("Service unavailable");
        response.RejectionEventType.ShouldBeNull();
    }

    [Fact]
    public async Task GetStatus_WhitespaceCorrelationId_Returns400() {
        // Arrange
        SetupHttpContext("tenant-a");

        // Act
        IActionResult result = await _controller.GetStatus("   ", CancellationToken.None);

        // Assert
        ObjectResult objResult = result.ShouldBeOfType<ObjectResult>();
        objResult.StatusCode.ShouldBe(400);
        ProblemDetails pd = objResult.Value.ShouldBeOfType<ProblemDetails>();
        pd.Detail.ShouldBe("Message or correlation identifier must be 1-128 characters of alphanumerics and hyphens (with alphanumeric anchors).");
        pd.Type.ShouldBe("https://hexalith.io/problems/bad-request");
        pd.Extensions.ShouldContainKey("correlationId");
        _controller.HttpContext.Response.ContentType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task GetStatus_TimedOutStatus_IncludesIso8601Duration() {
        // Arrange
        string correlationId = CorrelationId;
        SetupHttpContext("tenant-a");
        await _statusStore.WriteStatusAsync(
            "tenant-a", correlationId,
            new CommandStatusRecord(CommandStatus.TimedOut, DateTimeOffset.UtcNow, "agg-timeout", null, null, null, TimeSpan.FromSeconds(30)),
            CancellationToken.None);

        // Act
        IActionResult result = await _controller.GetStatus(correlationId, CancellationToken.None);

        // Assert
        OkObjectResult okResult = result.ShouldBeOfType<OkObjectResult>();
        CommandStatusResponse response = okResult.Value.ShouldBeOfType<CommandStatusResponse>();
        response.Status.ShouldBe("TimedOut");
        response.TimeoutDuration.ShouldBe("PT30S");
    }

    [Fact]
    public async Task GetStatus_NonTerminalStatus_IncludesRetryAfterHeader() {
        // Arrange
        string correlationId = CorrelationId;
        SetupHttpContext("tenant-a");
        await _statusStore.WriteStatusAsync(
            "tenant-a", correlationId,
            new CommandStatusRecord(CommandStatus.Received, DateTimeOffset.UtcNow, "agg-1", null, null, null, null),
            CancellationToken.None);

        // Act
        _ = await _controller.GetStatus(correlationId, CancellationToken.None);

        // Assert
        _controller.HttpContext.Response.Headers["Retry-After"].ToString().ShouldBe("1");
    }

    [Fact]
    public async Task GetStatus_ProcessingStatus_IncludesRetryAfterHeader() {
        // Arrange
        string correlationId = CorrelationId;
        SetupHttpContext("tenant-a");
        await _statusStore.WriteStatusAsync(
            "tenant-a", correlationId,
            new CommandStatusRecord(CommandStatus.Processing, DateTimeOffset.UtcNow, "agg-proc", null, null, null, null),
            CancellationToken.None);

        // Act
        _ = await _controller.GetStatus(correlationId, CancellationToken.None);

        // Assert
        _controller.HttpContext.Response.Headers["Retry-After"].ToString().ShouldBe("1");
    }

    [Fact]
    public async Task GetStatus_EventsStoredStatus_IncludesRetryAfterHeader() {
        // Arrange
        string correlationId = CorrelationId;
        SetupHttpContext("tenant-a");
        await _statusStore.WriteStatusAsync(
            "tenant-a", correlationId,
            new CommandStatusRecord(CommandStatus.EventsStored, DateTimeOffset.UtcNow, "agg-stored", null, null, null, null),
            CancellationToken.None);

        // Act
        _ = await _controller.GetStatus(correlationId, CancellationToken.None);

        // Assert
        _controller.HttpContext.Response.Headers["Retry-After"].ToString().ShouldBe("1");
    }

    [Fact]
    public async Task GetStatus_EventsPublishedStatus_IncludesRetryAfterHeader() {
        // Arrange
        string correlationId = CorrelationId;
        SetupHttpContext("tenant-a");
        await _statusStore.WriteStatusAsync(
            "tenant-a", correlationId,
            new CommandStatusRecord(CommandStatus.EventsPublished, DateTimeOffset.UtcNow, "agg-pub", null, null, null, null),
            CancellationToken.None);

        // Act
        _ = await _controller.GetStatus(correlationId, CancellationToken.None);

        // Assert
        _controller.HttpContext.Response.Headers["Retry-After"].ToString().ShouldBe("1");
    }

    [Fact]
    public async Task GetStatus_CompletedStatus_DoesNotIncludeRetryAfterHeader() {
        // Arrange
        string correlationId = CorrelationId;
        SetupHttpContext("tenant-a");
        await _statusStore.WriteStatusAsync(
            "tenant-a", correlationId,
            new CommandStatusRecord(CommandStatus.Completed, DateTimeOffset.UtcNow, "agg-done", 5, null, null, null),
            CancellationToken.None);

        // Act
        _ = await _controller.GetStatus(correlationId, CancellationToken.None);

        // Assert
        _controller.HttpContext.Response.Headers.ContainsKey("Retry-After").ShouldBeFalse();
    }

    [Fact]
    public async Task GetStatus_RejectedStatus_DoesNotIncludeRetryAfterHeader() {
        // Arrange
        string correlationId = CorrelationId;
        SetupHttpContext("tenant-a");
        await _statusStore.WriteStatusAsync(
            "tenant-a", correlationId,
            new CommandStatusRecord(CommandStatus.Rejected, DateTimeOffset.UtcNow, "agg-rej", null, "OrderRejected", null, null),
            CancellationToken.None);

        // Act
        _ = await _controller.GetStatus(correlationId, CancellationToken.None);

        // Assert
        _controller.HttpContext.Response.Headers.ContainsKey("Retry-After").ShouldBeFalse();
    }

    [Fact]
    public async Task GetStatus_TimedOutStatus_DoesNotIncludeRetryAfterHeader() {
        // Arrange
        string correlationId = CorrelationId;
        SetupHttpContext("tenant-a");
        await _statusStore.WriteStatusAsync(
            "tenant-a", correlationId,
            new CommandStatusRecord(CommandStatus.TimedOut, DateTimeOffset.UtcNow, "agg-timeout", null, null, null, TimeSpan.FromSeconds(30)),
            CancellationToken.None);

        // Act
        _ = await _controller.GetStatus(correlationId, CancellationToken.None);

        // Assert
        _controller.HttpContext.Response.Headers.ContainsKey("Retry-After").ShouldBeFalse();
    }

    [Fact]
    public async Task GetStatus_PublishFailedStatus_DoesNotIncludeRetryAfterHeader() {
        // Arrange
        string correlationId = CorrelationId;
        SetupHttpContext("tenant-a");
        await _statusStore.WriteStatusAsync(
            "tenant-a", correlationId,
            new CommandStatusRecord(CommandStatus.PublishFailed, DateTimeOffset.UtcNow, "agg-pubfail", null, null, "Pub/sub unavailable", null),
            CancellationToken.None);

        // Act
        _ = await _controller.GetStatus(correlationId, CancellationToken.None);

        // Assert
        _controller.HttpContext.Response.Headers.ContainsKey("Retry-After").ShouldBeFalse();
    }

    private void SetupHttpContext(string tenantId) => SetupHttpContextWithClaims([tenantId]);

    private void SetupHttpContextWithClaims(string[] tenants) {
        var claims = new List<Claim>
        {
            new("sub", "test-user"),
        };

        foreach (string tenant in tenants) {
            claims.Add(new Claim("eventstore:tenant", tenant));
        }

        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext {
            User = principal,
        };
        httpContext.Items["CorrelationId"] = Guid.NewGuid().ToString();

        _controller.ControllerContext = new ControllerContext {
            HttpContext = httpContext,
        };
    }

    private static CommandStatusController CreateController(
        ICommandStatusStore store,
        ICommandCorrelationIndex index,
        params string[] tenants) {
        var controller = new CommandStatusController(store, NullLogger<CommandStatusController>.Instance, index);
        var claims = new List<Claim> { new("sub", "test-user") };
        claims.AddRange(tenants.Select(tenant => new Claim("eventstore:tenant", tenant)));
        controller.ControllerContext = new ControllerContext {
            HttpContext = new DefaultHttpContext {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth")),
            },
        };
        return controller;
    }
}
