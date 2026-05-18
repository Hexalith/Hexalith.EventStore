using System.Security.Claims;

using Hexalith.EventStore.Admin.Abstractions.Models.Commands;
using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Models.Streams;
using Hexalith.EventStore.Admin.Abstractions.Services;
using Hexalith.EventStore.Admin.Server.Authorization;
using Hexalith.EventStore.Admin.Server.Controllers;
using Hexalith.EventStore.Admin.Server.Services;
using Hexalith.EventStore.Contracts.Problems;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Hexalith.EventStore.Admin.Server.Tests.Controllers;

public class AdminStreamsControllerTests {
    private readonly IStreamQueryService _service = Substitute.For<IStreamQueryService>();
    private readonly AdminStreamsController _sut;

    public AdminStreamsControllerTests() => _sut = new AdminStreamsController(_service, NullLogger<AdminStreamsController>.Instance) {
        ControllerContext = new ControllerContext {
            HttpContext = new DefaultHttpContext {
                User = CreatePrincipal("ReadOnly", "tenant-a"),
            },
        }
    };

    [Fact]
    public async Task GetRecentlyActiveStreams_ReturnsOk() {
        var expected = new PagedResult<StreamSummary>([], 0, null);
        _ = _service.GetRecentlyActiveStreamsAsync(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        IActionResult result = await _sut.GetRecentlyActiveStreams("tenant-a", null, 100);

        OkObjectResult okResult = result.ShouldBeOfType<OkObjectResult>();
        okResult.Value.ShouldBe(expected);
    }

    [Fact]
    public async Task GetRecentCommands_ReturnsOk() {
        var expected = new PagedResult<CommandSummary>([], 0, null);
        _ = _service.GetRecentCommandsAsync(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        IActionResult result = await _sut.GetRecentCommands("tenant-a", "Processing", "Create", 100);

        OkObjectResult okResult = result.ShouldBeOfType<OkObjectResult>();
        okResult.Value.ShouldBe(expected);
    }

    [Fact]
    public async Task GetAggregateState_NullResult_Returns404() {
        _ = _service.GetAggregateStateAtPositionAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns((AggregateStateSnapshot)null!);

        IActionResult result = await _sut.GetAggregateState("t", "d", "a", 1);

        ObjectResult objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task GetRecentlyActiveStreams_ServiceThrowsRpcException_Returns503() {
        _ = _service.GetRecentlyActiveStreamsAsync(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Throws(new Grpc.Core.RpcException(new Grpc.Core.Status(Grpc.Core.StatusCode.Unavailable, "test")));

        IActionResult result = await _sut.GetRecentlyActiveStreams("tenant-a", null, 100);

        ObjectResult objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(StatusCodes.Status503ServiceUnavailable);
    }

    [Fact]
    public async Task GetRecentCommands_ServiceThrowsHttpRequestException_Returns503() {
        _ = _service.GetRecentCommandsAsync(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Throws(new HttpRequestException("unavailable"));

        IActionResult result = await _sut.GetRecentCommands("tenant-a", "Processing", "Create", 100);

        ObjectResult objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(StatusCodes.Status503ServiceUnavailable);
    }

    [Fact]
    public async Task GetRecentlyActiveStreams_PropagatesCancellationToken() {
        using var cts = new CancellationTokenSource();
        var expected = new PagedResult<StreamSummary>([], 0, null);
        _ = _service.GetRecentlyActiveStreamsAsync(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<int>(), cts.Token)
            .Returns(expected);

        _ = await _sut.GetRecentlyActiveStreams("tenant-a", null, 100, cts.Token);

        _ = await _service.Received(1).GetRecentlyActiveStreamsAsync(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<int>(), cts.Token);
    }

    [Fact]
    public async Task GetAggregateState_NullResult_Returns404WithCorrelationId() {
        _ = _service.GetAggregateStateAtPositionAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns((AggregateStateSnapshot)null!);

        IActionResult result = await _sut.GetAggregateState("t", "d", "a", 1);

        ObjectResult objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
        ProblemDetails problemDetails = objectResult.Value.ShouldBeOfType<ProblemDetails>();
        problemDetails.Extensions.ShouldContainKey("correlationId");
    }

    [Fact]
    public async Task GetRecentlyActiveStreams_ServiceThrowsRpcException_Returns503WithCorrelationId() {
        _ = _service.GetRecentlyActiveStreamsAsync(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Throws(new Grpc.Core.RpcException(new Grpc.Core.Status(Grpc.Core.StatusCode.Unavailable, "test")));

        IActionResult result = await _sut.GetRecentlyActiveStreams("tenant-a", null, 100);

        ObjectResult objectResult = result.ShouldBeOfType<ObjectResult>();
        ProblemDetails problemDetails = objectResult.Value.ShouldBeOfType<ProblemDetails>();
        problemDetails.Extensions.ShouldContainKey("correlationId");
    }

    [Fact]
    public async Task GetRecentlyActiveStreams_UnexpectedException_Returns500WithCorrelationId() {
        _ = _service.GetRecentlyActiveStreamsAsync(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("unexpected"));

        IActionResult result = await _sut.GetRecentlyActiveStreams("tenant-a", null, 100);

        ObjectResult objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(StatusCodes.Status500InternalServerError);
        ProblemDetails problemDetails = objectResult.Value.ShouldBeOfType<ProblemDetails>();
        problemDetails.Extensions.ShouldContainKey("correlationId");
        problemDetails.Detail.ShouldBe("An unexpected error occurred."); // generic message, no exception details leaked
    }

    [Fact]
    public async Task GetAggregateBlame_ReturnsOk_WhenBlameAvailable() {
        var expected = new AggregateBlameView(
            "t", "d", "a", 5, DateTimeOffset.UtcNow,
            [new FieldProvenance("Count", "5", "4", 5, DateTimeOffset.UtcNow, "Incr", "c", "u")],
            false, false);
        _ = _service.GetAggregateBlameAsync("t", "d", "a", 5L, Arg.Any<CancellationToken>())
            .Returns(expected);

        IActionResult result = await _sut.GetAggregateBlame("t", "d", "a", 5);

        OkObjectResult okResult = result.ShouldBeOfType<OkObjectResult>();
        okResult.Value.ShouldBe(expected);
    }

    [Fact]
    public async Task GetAggregateBlame_InvalidAtParam_Returns400() {
        IActionResult result = await _sut.GetAggregateBlame("t", "d", "a", 0);

        ObjectResult objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task GetAggregateBlame_ServiceUnavailable_Returns503() {
        _ = _service.GetAggregateBlameAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long?>(), Arg.Any<CancellationToken>())
            .Throws(new Grpc.Core.RpcException(new Grpc.Core.Status(Grpc.Core.StatusCode.Unavailable, "test")));

        IActionResult result = await _sut.GetAggregateBlame("t", "d", "a", null);

        ObjectResult objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(StatusCodes.Status503ServiceUnavailable);
    }

    [Fact]
    public async Task GetEventStepFrame_ReturnsOk_WhenFrameAvailable() {
        DateTimeOffset timestamp = new(2026, 3, 27, 10, 0, 0, TimeSpan.Zero);
        var expected = new EventStepFrame(
            "t", "d", "a", 3, "CounterIncremented", timestamp,
            "corr-1", "cause-1", "user-1",
            "{\"Amount\":1}", "{\"Count\":3}",
            [new FieldChange("Count", "2", "3")], 5);
        _ = _service.GetEventStepFrameAsync("t", "d", "a", 3L, Arg.Any<CancellationToken>())
            .Returns(expected);

        IActionResult result = await _sut.GetEventStepFrame("t", "d", "a", 3);

        OkObjectResult okResult = result.ShouldBeOfType<OkObjectResult>();
        okResult.Value.ShouldBe(expected);
    }

    [Fact]
    public async Task GetEventStepFrame_InvalidAtParam_Returns400() {
        IActionResult result = await _sut.GetEventStepFrame("t", "d", "a", 0);

        ObjectResult objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task GetEventStepFrame_NegativeAtParam_Returns400() {
        IActionResult result = await _sut.GetEventStepFrame("t", "d", "a", -1);

        ObjectResult objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task GetEventStepFrame_ServiceThrowsArgumentException_Returns400() {
        _ = _service.GetEventStepFrameAsync("t", "d", "a", 999L, Arg.Any<CancellationToken>())
            .Throws(new ArgumentException("Sequence beyond stream"));

        IActionResult result = await _sut.GetEventStepFrame("t", "d", "a", 999);

        ObjectResult objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task GetEventStepFrame_ServiceUnavailable_Returns503() {
        _ = _service.GetEventStepFrameAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Throws(new Grpc.Core.RpcException(new Grpc.Core.Status(Grpc.Core.StatusCode.Unavailable, "test")));

        IActionResult result = await _sut.GetEventStepFrame("t", "d", "a", 1);

        ObjectResult objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(StatusCodes.Status503ServiceUnavailable);
    }

    [Fact]
    public async Task GetEventStepFrame_ProtectedUpstreamProblem_PreservesProblemDetails() {
        var problem = new ProblemDetails {
            Type = UnreadableProtectedDataProblem.TypeUri,
            Title = UnreadableProtectedDataProblem.DefaultTitle,
            Status = StatusCodes.Status503ServiceUnavailable,
            Detail = "Protection provider is temporarily unavailable. Retry later with backoff.",
        };
        problem.Extensions["reasonCode"] = "provider-unavailable";
        problem.Extensions["stage"] = "admin-inspection";
        _ = _service.GetEventStepFrameAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Throws(new AdminUpstreamProblemException(problem, System.Net.HttpStatusCode.ServiceUnavailable));

        IActionResult result = await _sut.GetEventStepFrame("t", "d", "a", 1);

        ObjectResult objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(StatusCodes.Status503ServiceUnavailable);
        ProblemDetails returned = objectResult.Value.ShouldBeOfType<ProblemDetails>();
        returned.Type.ShouldBe(UnreadableProtectedDataProblem.TypeUri);
        returned.Extensions["reasonCode"].ShouldBe("provider-unavailable");
        returned.Extensions["stage"].ShouldBe("admin-inspection");
    }

    [Fact]
    public async Task GetEventDetail_InvalidSequence_Returns400AndDoesNotCallService() {
        IActionResult result = await _sut.GetEventDetail("t", "d", "a", 0);

        ObjectResult objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        ProblemDetails problem = objectResult.Value.ShouldBeOfType<ProblemDetails>();
        problem.Detail.ShouldNotBeNull();
        problem.Detail.ShouldContain("'sequenceNumber' must be >= 1");
        _ = await _service.DidNotReceive().GetEventDetailAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetEventDetail_NegativeSequence_Returns400AndDoesNotCallService() {
        IActionResult result = await _sut.GetEventDetail("t", "d", "a", -5);

        ObjectResult objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        _ = await _service.DidNotReceive().GetEventDetailAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetEventDetail_KeyNotFoundException_Returns404WithEventNotFoundDetail() {
        _ = _service.GetEventDetailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Throws(new KeyNotFoundException("Event not found."));

        IActionResult result = await _sut.GetEventDetail("t", "d", "a", 5);

        ObjectResult objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
        ProblemDetails problem = objectResult.Value.ShouldBeOfType<ProblemDetails>();
        problem.Detail.ShouldBe("Event not found.");
    }

    [Fact]
    public async Task GetEventDetail_ArgumentException_Returns400() {
        _ = _service.GetEventDetailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Throws(new ArgumentException("Parameter 'sequenceNumber' must be >= 1."));

        IActionResult result = await _sut.GetEventDetail("t", "d", "a", 5);

        ObjectResult objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task GetEventDetail_HttpRequestException_Returns503() {
        _ = _service.GetEventDetailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Throws(new HttpRequestException("downstream 502"));

        IActionResult result = await _sut.GetEventDetail("t", "d", "a", 5);

        ObjectResult objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(StatusCodes.Status503ServiceUnavailable);
    }

    [Fact]
    public async Task GetEventDetail_ReturnsOk_WhenServiceReturnsDetail() {
        DateTimeOffset timestamp = new(2026, 5, 5, 12, 0, 0, TimeSpan.Zero);
        var expected = new EventDetail("t", "d", "a", 5, "CounterIncremented", timestamp, "corr-1", "cause-1", "user-1", "{\"value\":1}");
        _ = _service.GetEventDetailAsync("t", "d", "a", 5L, Arg.Any<CancellationToken>())
            .Returns(expected);

        IActionResult result = await _sut.GetEventDetail("t", "d", "a", 5);

        OkObjectResult ok = result.ShouldBeOfType<OkObjectResult>();
        ok.Value.ShouldBe(expected);
    }

    private static ClaimsPrincipal CreatePrincipal(string adminRole, params string[] tenants) {
        var claims = new List<Claim> { new(AdminClaimTypes.AdminRole, adminRole) };
        foreach (string tenant in tenants) {
            claims.Add(new Claim(AdminClaimTypes.Tenant, tenant));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }
}
