
using System.Security.Claims;

using Hexalith.EventStore.CommandApi.Controllers;
using Hexalith.EventStore.CommandApi.Models;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Testing.Fakes;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

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
        pd.Detail.ShouldBe("Correlation ID is required.");
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
}
