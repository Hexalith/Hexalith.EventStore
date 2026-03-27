using System.Security.Claims;

using Hexalith.EventStore.Admin.Abstractions.Models.Streams;
using Hexalith.EventStore.Admin.Abstractions.Services;
using Hexalith.EventStore.Admin.Server.Authorization;
using Hexalith.EventStore.Admin.Server.Controllers;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Hexalith.EventStore.Admin.Server.Tests.Controllers;

public class AdminStreamsControllerSandboxTests
{
    private readonly IStreamQueryService _service = Substitute.For<IStreamQueryService>();
    private readonly AdminStreamsController _sut;

    public AdminStreamsControllerSandboxTests()
    {
        _sut = new AdminStreamsController(_service, NullLogger<AdminStreamsController>.Instance);
        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(AdminClaimTypes.AdminRole, "Admin"),
                    new Claim(AdminClaimTypes.Tenant, "tenant1"),
                ], "TestAuth")),
                Items = { ["CorrelationId"] = "test-correlation" },
            },
        };
    }

    [Fact]
    public async Task SandboxCommand_WithEmptyCommandType_Returns400()
    {
        var request = new SandboxCommandRequest(string.Empty, "{}", null, null, null);

        IActionResult result = await _sut.SandboxCommand("tenant1", "orders", "order-1", request);

        var objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        var problemDetails = objectResult.Value.ShouldBeOfType<ProblemDetails>();
        problemDetails.Detail.ShouldNotBeNull();
        problemDetails.Detail!.ShouldContain("CommandType");
    }

    [Fact]
    public async Task SandboxCommand_WithValidRequest_ReturnsOkWithResult()
    {
        var request = new SandboxCommandRequest("IncrementCounter", "{\"Amount\":1}", 5L, "corr-1", "user-1");
        var expected = new SandboxResult(
            "tenant1",
            "orders",
            "order-1",
            5L,
            "IncrementCounter",
            "accepted",
            [new SandboxEvent(0, "CounterIncremented", "{\"Amount\":1}", false)],
            "{\"Count\":6}",
            [new FieldChange("Count", "5", "6")],
            null,
            12L);

        _service.SandboxCommandAsync(
            "tenant1", "orders", "order-1", request, Arg.Any<CancellationToken>())
            .Returns(expected);

        IActionResult result = await _sut.SandboxCommand("tenant1", "orders", "order-1", request);

        var okResult = result.ShouldBeOfType<OkObjectResult>();
        okResult.Value.ShouldBe(expected);
    }

    [Fact]
    public async Task SandboxCommand_WithNullResult_Returns404()
    {
        var request = new SandboxCommandRequest("IncrementCounter", "{}", null, null, null);

        _service.SandboxCommandAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<SandboxCommandRequest>(), Arg.Any<CancellationToken>())
            .Returns((SandboxResult?)null);

        IActionResult result = await _sut.SandboxCommand("tenant1", "orders", "order-1", request);

        var objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
        var problemDetails = objectResult.Value.ShouldBeOfType<ProblemDetails>();
        problemDetails.Extensions.ShouldContainKey("correlationId");
    }

    [Fact]
    public async Task SandboxCommand_ServiceUnavailable_Returns503()
    {
        var request = new SandboxCommandRequest("IncrementCounter", "{}", null, null, null);

        _service.SandboxCommandAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<SandboxCommandRequest>(), Arg.Any<CancellationToken>())
            .Throws(new HttpRequestException("Connection refused"));

        IActionResult result = await _sut.SandboxCommand("tenant1", "orders", "order-1", request);

        var objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(StatusCodes.Status503ServiceUnavailable);
        var problemDetails = objectResult.Value.ShouldBeOfType<ProblemDetails>();
        problemDetails.Extensions.ShouldContainKey("correlationId");
    }

    [Fact]
    public async Task SandboxCommand_UnexpectedError_Returns500()
    {
        var request = new SandboxCommandRequest("IncrementCounter", "{}", null, null, null);

        _service.SandboxCommandAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<SandboxCommandRequest>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("Something broke"));

        IActionResult result = await _sut.SandboxCommand("tenant1", "orders", "order-1", request);

        var objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(StatusCodes.Status500InternalServerError);
        var problemDetails = objectResult.Value.ShouldBeOfType<ProblemDetails>();
        problemDetails.Extensions.ShouldContainKey("correlationId");
        problemDetails.Detail.ShouldBe("An unexpected error occurred.");
    }

    [Fact]
    public async Task SandboxCommand_WithNullRequestBody_Returns400()
    {
        IActionResult result = await _sut.SandboxCommand("tenant1", "orders", "order-1", null!);

        var objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        var problemDetails = objectResult.Value.ShouldBeOfType<ProblemDetails>();
        problemDetails.Detail.ShouldNotBeNull();
        problemDetails.Detail!.ShouldContain("Request body");
    }

    [Fact]
    public async Task SandboxCommand_WithAtSequenceZero_ReturnsOkWithResult()
    {
        var request = new SandboxCommandRequest("CreateOrder", "{}", 0L, null, null);
        var expected = new SandboxResult(
            "tenant1",
            "orders",
            "order-1",
            0L,
            "CreateOrder",
            "accepted",
            [new SandboxEvent(0, "OrderCreated", "{\"Id\":\"order-1\"}", false)],
            "{\"Id\":\"order-1\"}",
            [new FieldChange("Id", string.Empty, "\"order-1\"")],
            null,
            5L);

        _service.SandboxCommandAsync(
            "tenant1", "orders", "order-1", request, Arg.Any<CancellationToken>())
            .Returns(expected);

        IActionResult result = await _sut.SandboxCommand("tenant1", "orders", "order-1", request);

        var okResult = result.ShouldBeOfType<OkObjectResult>();
        var sandboxResult = okResult.Value.ShouldBeOfType<SandboxResult>();
        sandboxResult.AtSequence.ShouldBe(0L);
        sandboxResult.Outcome.ShouldBe("accepted");
    }
}
