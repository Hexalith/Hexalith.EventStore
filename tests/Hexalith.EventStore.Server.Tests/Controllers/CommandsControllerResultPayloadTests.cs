using System.Security.Claims;
using System.Text.Json;

using Hexalith.EventStore.Configuration;
using Hexalith.EventStore.Controllers;
using Hexalith.EventStore.Middleware;
using Hexalith.EventStore.Models;
using Hexalith.EventStore.Server.Pipeline.Commands;
using Hexalith.EventStore.Validation;

using MediatR;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Controllers;

/// <summary>
/// ES-1 DoS-guard tests for <see cref="CommandsController"/>.ParseOptionalResultPayload, exercised
/// through the public <c>Submit</c> path with a mocked <see cref="IMediator"/> returning a crafted
/// <see cref="SubmitCommandResult.ResultPayload"/>. Proves the explicit MaxDepth=64 bound and the
/// pre-parse length cap, the no-payload-content drop warning, and that valid payloads still
/// round-trip unchanged.
/// </summary>
public class CommandsControllerResultPayloadTests {
    // Mirror of the private CommandsController.MaxResultPayloadCharacters constant (64 KiB).
    private const int MaxResultPayloadCharacters = 64 * 1024;

    [Fact]
    public async Task Submit_OversizedResultPayload_DropsPayloadAndLogsLengthCapWarning() {
        // Arrange — a VALID JSON string longer than the cap. The old unbounded code would parse and
        // return a non-null JsonValueKind.String element; this assertion is therefore regression-sensitive.
        string oversized = "\"" + new string('x', MaxResultPayloadCharacters + 4_000) + "\"";
        oversized.Length.ShouldBeGreaterThan(MaxResultPayloadCharacters);
        (CommandsController controller, CapturingLogger<CommandsController> logger) = CreateController(oversized);

        // Act
        IActionResult result = await controller.Submit(CreateTestRequest(), CancellationToken.None);

        // Assert — payload dropped, not parsed
        GetResultPayload(result).ShouldBeNull();

        // Exactly one warning, carrying correlation id + numeric lengths only, never payload content.
        (int warningCount, string? warningMessage) = SingleWarning(logger);
        warningCount.ShouldBe(1);
        _ = warningMessage.ShouldNotBeNull();
        warningMessage.ShouldContain("test-correlation-id");
        warningMessage.ShouldContain("parse cap");
        warningMessage.ShouldContain(MaxResultPayloadCharacters.ToString());
        warningMessage.ShouldContain(oversized.Length.ToString());
        warningMessage.ShouldNotContain("xxxx");
    }

    [Fact]
    public async Task Submit_OverDepthResultPayload_ReturnsNullViaJsonExceptionPath() {
        // Arrange — 65 nested arrays exceed MaxDepth=64; in-bound length, but JsonDocument.Parse throws.
        const int depth = 65;
        string overDepth = new string('[', depth) + new string(']', depth);
        overDepth.Length.ShouldBeLessThan(MaxResultPayloadCharacters);
        (CommandsController controller, CapturingLogger<CommandsController> logger) = CreateController(overDepth);

        // Act
        IActionResult result = await controller.Submit(CreateTestRequest(), CancellationToken.None);

        // Assert — handled by the existing catch (JsonException) branch, not the length cap.
        GetResultPayload(result).ShouldBeNull();
        (int warningCount, string? warningMessage) = SingleWarning(logger);
        warningCount.ShouldBe(1);
        _ = warningMessage.ShouldNotBeNull();
        warningMessage.ShouldContain("could not be parsed as JSON");
        warningMessage.ShouldNotContain("parse cap");
    }

    [Fact]
    public async Task Submit_ValidObjectResultPayload_PreservedInResponse() {
        // Arrange
        (CommandsController controller, _) = CreateController("""{"orderId":"abc","total":42}""");

        // Act
        IActionResult result = await controller.Submit(CreateTestRequest(), CancellationToken.None);

        // Assert
        JsonElement payload = GetResultPayload(result).ShouldNotBeNull();
        payload.ValueKind.ShouldBe(JsonValueKind.Object);
        payload.GetProperty("orderId").GetString().ShouldBe("abc");
        payload.GetProperty("total").GetInt32().ShouldBe(42);
    }

    [Fact]
    public async Task Submit_DistinctMessageAndCorrelation_ReturnsMessageTrackingLocation() {
        (CommandsController controller, _) = CreateController(null);
        SubmitCommandRequest request = CreateTestRequest();

        IActionResult result = await controller.Submit(request, CancellationToken.None);

        AcceptedResult accepted = result.ShouldBeOfType<AcceptedResult>();
        var response = accepted.Value.ShouldBeOfType<Hexalith.EventStore.Contracts.Commands.SubmitCommandResponse>();
        response.CorrelationId.ShouldBe("test-correlation-id");
        response.MessageId.ShouldBe(request.MessageId);
        controller.Response.Headers.Location.ToString().ShouldBe(
            $"https://localhost/api/v1/commands/status/{request.MessageId}");
        controller.Response.Headers.Location.ToString().ShouldNotContain(response.CorrelationId);
    }

    [Fact]
    public async Task Submit_ValidArrayResultPayload_PreservedInResponse() {
        // Arrange
        (CommandsController controller, _) = CreateController("[1,2,3]");

        // Act
        IActionResult result = await controller.Submit(CreateTestRequest(), CancellationToken.None);

        // Assert
        JsonElement payload = GetResultPayload(result).ShouldNotBeNull();
        payload.ValueKind.ShouldBe(JsonValueKind.Array);
        payload.GetArrayLength().ShouldBe(3);
    }

    [Fact]
    public async Task Submit_ValidScalarResultPayload_PreservedInResponse() {
        // Arrange
        (CommandsController controller, _) = CreateController("42");

        // Act
        IActionResult result = await controller.Submit(CreateTestRequest(), CancellationToken.None);

        // Assert
        JsonElement payload = GetResultPayload(result).ShouldNotBeNull();
        payload.ValueKind.ShouldBe(JsonValueKind.Number);
        payload.GetInt32().ShouldBe(42);
    }

    [Fact]
    public async Task Submit_JsonNullResultPayload_PreservedAsNullKind() {
        // Arrange — a JSON null literal is a well-formed, in-bound payload (not the same as a missing string).
        (CommandsController controller, _) = CreateController("null");

        // Act
        IActionResult result = await controller.Submit(CreateTestRequest(), CancellationToken.None);

        // Assert — preserved with ValueKind.Null (only JsonValueKind.Undefined collapses to no payload).
        JsonElement payload = GetResultPayload(result).ShouldNotBeNull();
        payload.ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Submit_NullOrWhitespaceResultPayload_ReturnsNull(string? resultPayload) {
        // Arrange
        (CommandsController controller, CapturingLogger<CommandsController> logger) = CreateController(resultPayload);

        // Act
        IActionResult result = await controller.Submit(CreateTestRequest(), CancellationToken.None);

        // Assert — early return, no parse, no warning.
        GetResultPayload(result).ShouldBeNull();
        SingleWarning(logger).WarningCount.ShouldBe(0);
    }

    [Fact]
    public async Task Submit_MalformedInBoundResultPayload_ReturnsNullViaJsonExceptionPath() {
        // Arrange — short, in-bound, in-depth, but not valid JSON.
        (CommandsController controller, CapturingLogger<CommandsController> logger) = CreateController("{not valid json");

        // Act
        IActionResult result = await controller.Submit(CreateTestRequest(), CancellationToken.None);

        // Assert — existing malformed-JSON warning, NOT the length-cap branch.
        GetResultPayload(result).ShouldBeNull();
        (int warningCount, string? warningMessage) = SingleWarning(logger);
        warningCount.ShouldBe(1);
        _ = warningMessage.ShouldNotBeNull();
        warningMessage.ShouldContain("could not be parsed as JSON");
        warningMessage.ShouldNotContain("parse cap");
    }

    private static (CommandsController Controller, CapturingLogger<CommandsController> Logger) CreateController(string? resultPayload) {
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>())
            .Returns(new SubmitCommandResult("test-correlation-id", resultPayload));

        ExtensionMetadataSanitizer sanitizer = new(Options.Create(new ExtensionMetadataOptions()));
        var logger = new CapturingLogger<CommandsController>();
        var controller = new CommandsController(mediator, sanitizer, logger);

        var httpContext = new DefaultHttpContext {
            User = CreateAuthenticatedPrincipal("test-tenant"),
        };
        httpContext.Items[CorrelationIdMiddleware.HttpContextKey] = "test-correlation-id";
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("localhost");
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        return (controller, logger);
    }

    private static SubmitCommandRequest CreateTestRequest() =>
        new(
            MessageId: Guid.NewGuid().ToString(),
            Tenant: "test-tenant",
            Domain: "test-domain",
            AggregateId: "agg-001",
            CommandType: "CreateOrder",
            Payload: JsonSerializer.SerializeToElement(new { Value = 1 }),
            Extensions: null);

    private static ClaimsPrincipal CreateAuthenticatedPrincipal(string tenant) =>
        new(new ClaimsIdentity(
        [
            new Claim("sub", "test-user"),
            new Claim("eventstore:tenant", tenant),
        ],
        "test"));

    private static JsonElement? GetResultPayload(IActionResult result) {
        AcceptedResult accepted = result.ShouldBeOfType<AcceptedResult>();
        var response = accepted.Value.ShouldBeOfType<Hexalith.EventStore.Contracts.Commands.SubmitCommandResponse>();
        return response.ResultPayload;
    }

    private static (int WarningCount, string? LastWarning) SingleWarning(CapturingLogger<CommandsController> logger) {
        int warningCount = 0;
        string? lastWarning = null;
        foreach ((LogLevel level, string message) in logger.Entries) {
            if (level == LogLevel.Warning) {
                warningCount++;
                lastWarning = message;
            }
        }

        return (warningCount, lastWarning);
    }

    private sealed class CapturingLogger<T> : ILogger<T> {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) {
            ArgumentNullException.ThrowIfNull(formatter);
            Entries.Add((logLevel, formatter(state, exception)));
        }
    }
}
