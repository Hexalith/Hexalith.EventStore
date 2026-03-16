
using System.Security.Claims;

using Hexalith.EventStore.CommandApi.ErrorHandling;
using Hexalith.EventStore.CommandApi.Controllers;
using Hexalith.EventStore.CommandApi.Models;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Server.Pipeline.Commands;
using Hexalith.EventStore.Testing.Fakes;

using MediatR;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Commands;

public class ReplayControllerTests {
    private readonly InMemoryCommandArchiveStore _archiveStore = new();
    private readonly InMemoryCommandStatusStore _statusStore = new();
    private readonly IMediator _mediator = Substitute.For<IMediator>();

    private ReplayController CreateController(ClaimsPrincipal? user = null) {
        var controller = new ReplayController(
            _archiveStore,
            _statusStore,
            _mediator,
            NullLogger<ReplayController>.Instance);

        var httpContext = new DefaultHttpContext {
            User = user ?? CreateUserWithTenants("tenant-a"),
        };
        httpContext.Items["CorrelationId"] = "request-corr-id";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        return controller;
    }

    private static ClaimsPrincipal CreateUserWithTenants(params string[] tenants) {
        var claims = new List<Claim>
        {
            new("sub", "test-user"),
        };
        claims.AddRange(tenants.Select(t => new Claim("eventstore:tenant", t)));

        var identity = new ClaimsIdentity(claims, "TestAuth");
        return new ClaimsPrincipal(identity);
    }

    private async Task SeedArchivedCommand(string tenant, string correlationId, CommandStatus status) {
        var archived = new ArchivedCommand(
            Tenant: tenant,
            Domain: "orders",
            AggregateId: "agg-001",
            CommandType: "CreateOrder",
            Payload: [1, 2, 3],
            Extensions: null,
            OriginalTimestamp: DateTimeOffset.UtcNow);

        await _archiveStore.WriteCommandAsync(tenant, correlationId, archived, CancellationToken.None).ConfigureAwait(false);
        await _statusStore.WriteStatusAsync(
            tenant,
            correlationId,
            new CommandStatusRecord(status, DateTimeOffset.UtcNow, "agg-001", null, null, null, null),
            CancellationToken.None).ConfigureAwait(false);
    }

    [Fact]
    public async Task Replay_RejectedStatus_Returns202WithReplayResponse() {
        // Arrange
        string correlationId = Guid.NewGuid().ToString();
        await SeedArchivedCommand("tenant-a", correlationId, CommandStatus.Rejected);

        _ = _mediator.Send(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => new SubmitCommandResult(callInfo.Arg<SubmitCommand>().CorrelationId));

        ReplayController controller = CreateController();

        // Act
        IActionResult result = await controller.Replay(correlationId, CancellationToken.None);

        // Assert — replay generates a new correlation ID distinct from the original
        AcceptedResult accepted = result.ShouldBeOfType<AcceptedResult>();
        ReplayCommandResponse response = accepted.Value.ShouldBeOfType<ReplayCommandResponse>();
        response.CorrelationId.ShouldNotBe(correlationId);
        Guid.TryParse(response.CorrelationId, out _).ShouldBeTrue();
        response.IsReplay.ShouldBeTrue();
        response.PreviousStatus.ShouldBe("Rejected");
        response.OriginalCorrelationId.ShouldBe(correlationId);
    }

    [Fact]
    public async Task Replay_PublishFailedStatus_Returns202() {
        // Arrange
        string correlationId = Guid.NewGuid().ToString();
        await SeedArchivedCommand("tenant-a", correlationId, CommandStatus.PublishFailed);

        _ = _mediator.Send(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>())
            .Returns(new SubmitCommandResult(correlationId));

        ReplayController controller = CreateController();

        // Act
        IActionResult result = await controller.Replay(correlationId, CancellationToken.None);

        // Assert
        AcceptedResult accepted = result.ShouldBeOfType<AcceptedResult>();
        ReplayCommandResponse response = accepted.Value.ShouldBeOfType<ReplayCommandResponse>();
        response.IsReplay.ShouldBeTrue();
        response.PreviousStatus.ShouldBe("PublishFailed");
        response.OriginalCorrelationId.ShouldBe(correlationId);
    }

    [Fact]
    public async Task Replay_TimedOutStatus_Returns202() {
        // Arrange
        string correlationId = Guid.NewGuid().ToString();
        await SeedArchivedCommand("tenant-a", correlationId, CommandStatus.TimedOut);

        _ = _mediator.Send(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>())
            .Returns(new SubmitCommandResult(correlationId));

        ReplayController controller = CreateController();

        // Act
        IActionResult result = await controller.Replay(correlationId, CancellationToken.None);

        // Assert
        AcceptedResult accepted = result.ShouldBeOfType<AcceptedResult>();
        ReplayCommandResponse response = accepted.Value.ShouldBeOfType<ReplayCommandResponse>();
        response.IsReplay.ShouldBeTrue();
        response.PreviousStatus.ShouldBe("TimedOut");
        response.OriginalCorrelationId.ShouldBe(correlationId);
    }

    [Fact]
    public async Task Replay_CompletedStatus_Returns409ProblemDetails() {
        // Arrange
        string correlationId = Guid.NewGuid().ToString();
        await SeedArchivedCommand("tenant-a", correlationId, CommandStatus.Completed);
        ReplayController controller = CreateController();

        // Act
        IActionResult result = await controller.Replay(correlationId, CancellationToken.None);

        // Assert
        ObjectResult objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(409);
        ProblemDetails problemDetails = objectResult.Value.ShouldBeOfType<ProblemDetails>();
        problemDetails.Detail!.ShouldContain("completed successfully");
        problemDetails.Extensions["currentStatus"]!.ToString().ShouldBe("Completed");
    }

    [Fact]
    public async Task Replay_ProcessingStatus_Returns409ProblemDetails() {
        // Arrange
        string correlationId = Guid.NewGuid().ToString();
        await SeedArchivedCommand("tenant-a", correlationId, CommandStatus.Processing);
        ReplayController controller = CreateController();

        // Act
        IActionResult result = await controller.Replay(correlationId, CancellationToken.None);

        // Assert
        ObjectResult objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(409);
        ProblemDetails problemDetails = objectResult.Value.ShouldBeOfType<ProblemDetails>();
        problemDetails.Detail!.ShouldContain("in-flight");
    }

    [Fact]
    public async Task Replay_ReceivedStatus_Returns409ProblemDetails() {
        // Arrange
        string correlationId = Guid.NewGuid().ToString();
        await SeedArchivedCommand("tenant-a", correlationId, CommandStatus.Received);
        ReplayController controller = CreateController();

        // Act
        IActionResult result = await controller.Replay(correlationId, CancellationToken.None);

        // Assert
        ObjectResult objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(409);
    }

    [Fact]
    public async Task Replay_EventsStoredStatus_Returns409ProblemDetails() {
        // Arrange
        string correlationId = Guid.NewGuid().ToString();
        await SeedArchivedCommand("tenant-a", correlationId, CommandStatus.EventsStored);
        ReplayController controller = CreateController();

        // Act
        IActionResult result = await controller.Replay(correlationId, CancellationToken.None);

        // Assert
        ObjectResult objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(409);
        ProblemDetails problemDetails = objectResult.Value.ShouldBeOfType<ProblemDetails>();
        problemDetails.Detail!.ShouldContain("in-flight");
    }

    [Fact]
    public async Task Replay_EventsPublishedStatus_Returns409ProblemDetails() {
        // Arrange
        string correlationId = Guid.NewGuid().ToString();
        await SeedArchivedCommand("tenant-a", correlationId, CommandStatus.EventsPublished);
        ReplayController controller = CreateController();

        // Act
        IActionResult result = await controller.Replay(correlationId, CancellationToken.None);

        // Assert
        ObjectResult objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(409);
        ProblemDetails problemDetails = objectResult.Value.ShouldBeOfType<ProblemDetails>();
        problemDetails.Detail!.ShouldContain("in-flight");
    }

    [Fact]
    public async Task Replay_NonExistentCorrelationId_Returns404ProblemDetails() {
        // Arrange
        string correlationId = Guid.NewGuid().ToString();
        ReplayController controller = CreateController();

        // Act
        IActionResult result = await controller.Replay(correlationId, CancellationToken.None);

        // Assert
        ObjectResult objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(404);
        ProblemDetails problemDetails = objectResult.Value.ShouldBeOfType<ProblemDetails>();
        problemDetails.Detail!.ShouldContain(correlationId);
    }

    [Fact]
    public async Task Replay_TenantMismatch_Returns404ProblemDetails() {
        // Arrange - archived as tenant-a, requesting as tenant-b
        string correlationId = Guid.NewGuid().ToString();
        await SeedArchivedCommand("tenant-a", correlationId, CommandStatus.Rejected);

        ReplayController controller = CreateController(CreateUserWithTenants("tenant-b"));

        // Act
        IActionResult result = await controller.Replay(correlationId, CancellationToken.None);

        // Assert - SEC-3: returns 404, not 403
        ObjectResult objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(404);
    }

    [Fact]
    public async Task Replay_NoTenantClaims_Returns403ProblemDetails() {
        // Arrange - user with no tenant claims
        string correlationId = Guid.NewGuid().ToString();
        var userWithNoClaims = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "test-user")], "TestAuth"));

        ReplayController controller = CreateController(userWithNoClaims);

        // Act
        IActionResult result = await controller.Replay(correlationId, CancellationToken.None);

        // Assert
        ObjectResult objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(403);
        ProblemDetails problemDetails = objectResult.Value.ShouldBeOfType<ProblemDetails>();
        problemDetails.Detail!.ShouldContain("No tenant authorization claims found");
    }

    [Fact]
    public async Task Replay_StatusResetByHandler() {
        // Arrange
        string correlationId = Guid.NewGuid().ToString();
        await SeedArchivedCommand("tenant-a", correlationId, CommandStatus.Rejected);

        _ = _mediator.Send(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>())
            .Returns(new SubmitCommandResult(correlationId));

        ReplayController controller = CreateController();

        // Act
        _ = await controller.Replay(correlationId, CancellationToken.None);

        // Assert - mediator.Send was called (handler resets status)
        _ = await _mediator.Received(1).Send(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Replay_ResubmitsThroughMediatRPipeline() {
        // Arrange
        string correlationId = Guid.NewGuid().ToString();
        await SeedArchivedCommand("tenant-a", correlationId, CommandStatus.Rejected);

        _ = _mediator.Send(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>())
            .Returns(new SubmitCommandResult(correlationId));

        ReplayController controller = CreateController();

        // Act
        _ = await controller.Replay(correlationId, CancellationToken.None);

        // Assert — replay generates a new correlation ID, so just verify Send was called with correct tenant
        _ = await _mediator.Received(1).Send(
            Arg.Is<SubmitCommand>(c => c.Tenant == "tenant-a"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Replay_GeneratesNewCorrelationId() {
        // Arrange
        string correlationId = Guid.NewGuid().ToString();
        await SeedArchivedCommand("tenant-a", correlationId, CommandStatus.Rejected);

        SubmitCommand? capturedCommand = null;
        _ = _mediator.Send(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => {
                capturedCommand = callInfo.Arg<SubmitCommand>();
                return new SubmitCommandResult(callInfo.Arg<SubmitCommand>().CorrelationId);
            });

        ReplayController controller = CreateController();

        // Act
        _ = await controller.Replay(correlationId, CancellationToken.None);

        // Assert — replay should generate a new correlation ID, not reuse the original
        _ = capturedCommand.ShouldNotBeNull();
        capturedCommand.CorrelationId.ShouldNotBe(correlationId);
        Guid.TryParse(capturedCommand.CorrelationId, out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Replay_NullStatus_ArchiveExists_Returns409ProblemDetails() {
        // Arrange - archive exists but status is null (expired)
        string correlationId = Guid.NewGuid().ToString();
        var archived = new ArchivedCommand(
            "tenant-a", "orders", "agg-001", "CreateOrder", [1, 2, 3], null, DateTimeOffset.UtcNow);
        await _archiveStore.WriteCommandAsync("tenant-a", correlationId, archived, CancellationToken.None);
        // No status written -- simulates expired status

        ReplayController controller = CreateController();

        // Act
        IActionResult result = await controller.Replay(correlationId, CancellationToken.None);

        // Assert - H5: null status returns 409 (indeterminate)
        ObjectResult objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(409);
        ProblemDetails problemDetails = objectResult.Value.ShouldBeOfType<ProblemDetails>();
        problemDetails.Detail!.ShouldContain("expired");
        problemDetails.Extensions["currentStatus"]!.ToString().ShouldBe("Unknown");
    }

    [Fact]
    public async Task Replay_CorruptedArchive_NullPayload_Returns500ProblemDetails() {
        // Arrange - archived command with empty payload (simulates corrupted state store deserialization)
        string correlationId = Guid.NewGuid().ToString();
        var corrupted = new ArchivedCommand(
            Tenant: "tenant-a",
            Domain: "orders",
            AggregateId: "agg-001",
            CommandType: "CreateOrder",
            Payload: [],
            Extensions: null,
            OriginalTimestamp: DateTimeOffset.UtcNow);

        await _archiveStore.WriteCommandAsync("tenant-a", correlationId, corrupted, CancellationToken.None);
        await _statusStore.WriteStatusAsync(
            "tenant-a",
            correlationId,
            new CommandStatusRecord(CommandStatus.Rejected, DateTimeOffset.UtcNow, "agg-001", null, null, null, null),
            CancellationToken.None);

        ReplayController controller = CreateController();

        // Act
        IActionResult result = await controller.Replay(correlationId, CancellationToken.None);

        // Assert - graceful error, not unhandled exception
        ObjectResult objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(500);
        ProblemDetails problemDetails = objectResult.Value.ShouldBeOfType<ProblemDetails>();
        problemDetails.Type.ShouldBe(ProblemTypeUris.InternalServerError);
        problemDetails.Detail!.ShouldContain("invalid");
    }

    [Fact]
    public async Task Replay_CorruptedArchive_NullCommandType_Returns500ProblemDetails() {
        // Arrange - archived command with null CommandType (simulates corrupted state store deserialization)
        string correlationId = Guid.NewGuid().ToString();
        var corrupted = new ArchivedCommand(
            Tenant: "tenant-a",
            Domain: "orders",
            AggregateId: "agg-001",
            CommandType: null!,
            Payload: [1, 2, 3],
            Extensions: null,
            OriginalTimestamp: DateTimeOffset.UtcNow);

        await _archiveStore.WriteCommandAsync("tenant-a", correlationId, corrupted, CancellationToken.None);
        await _statusStore.WriteStatusAsync(
            "tenant-a",
            correlationId,
            new CommandStatusRecord(CommandStatus.Rejected, DateTimeOffset.UtcNow, "agg-001", null, null, null, null),
            CancellationToken.None);

        ReplayController controller = CreateController();

        // Act
        IActionResult result = await controller.Replay(correlationId, CancellationToken.None);

        // Assert - graceful error, not unhandled exception
        ObjectResult objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(500);
        ProblemDetails problemDetails = objectResult.Value.ShouldBeOfType<ProblemDetails>();
        problemDetails.Type.ShouldBe(ProblemTypeUris.InternalServerError);
        problemDetails.Detail!.ShouldContain("invalid");
    }

    [Fact]
    public async Task Replay_CorruptedArchive_TenantMismatch_Returns500ProblemDetails() {
        // Arrange - archive key tenant differs from archived tenant content
        string correlationId = Guid.NewGuid().ToString();
        var corrupted = new ArchivedCommand(
            Tenant: "tenant-b",
            Domain: "orders",
            AggregateId: "agg-001",
            CommandType: "CreateOrder",
            Payload: [1, 2, 3],
            Extensions: null,
            OriginalTimestamp: DateTimeOffset.UtcNow);

        await _archiveStore.WriteCommandAsync("tenant-a", correlationId, corrupted, CancellationToken.None);
        await _statusStore.WriteStatusAsync(
            "tenant-a",
            correlationId,
            new CommandStatusRecord(CommandStatus.Rejected, DateTimeOffset.UtcNow, "agg-001", null, null, null, null),
            CancellationToken.None);

        ReplayController controller = CreateController();

        // Act
        IActionResult result = await controller.Replay(correlationId, CancellationToken.None);

        // Assert - deterministic corruption response
        ObjectResult objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(500);
        ProblemDetails problemDetails = objectResult.Value.ShouldBeOfType<ProblemDetails>();
        problemDetails.Type.ShouldBe(ProblemTypeUris.InternalServerError);
        problemDetails.Detail!.ShouldContain("invalid");
        problemDetails.Detail!.ShouldNotContain("tenant mismatch");
    }

    [Fact]
    public async Task Replay_CorruptedArchive_MissingAggregateIdentityFields_Returns500ProblemDetails() {
        // Arrange - null domain and aggregate id simulate deserialization corruption
        string correlationId = Guid.NewGuid().ToString();
        var corrupted = new ArchivedCommand(
            Tenant: "tenant-a",
            Domain: null!,
            AggregateId: null!,
            CommandType: "CreateOrder",
            Payload: [1, 2, 3],
            Extensions: null,
            OriginalTimestamp: DateTimeOffset.UtcNow);

        await _archiveStore.WriteCommandAsync("tenant-a", correlationId, corrupted, CancellationToken.None);
        await _statusStore.WriteStatusAsync(
            "tenant-a",
            correlationId,
            new CommandStatusRecord(CommandStatus.Rejected, DateTimeOffset.UtcNow, "agg-001", null, null, null, null),
            CancellationToken.None);

        ReplayController controller = CreateController();

        // Act
        IActionResult result = await controller.Replay(correlationId, CancellationToken.None);

        // Assert - deterministic corruption response
        ObjectResult objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(500);
        ProblemDetails problemDetails = objectResult.Value.ShouldBeOfType<ProblemDetails>();
        problemDetails.Type.ShouldBe(ProblemTypeUris.InternalServerError);
        problemDetails.Detail!.ShouldContain("invalid");
    }
}
