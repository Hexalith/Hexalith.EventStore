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

public class AdminTracesControllerTests {
    private readonly IStreamQueryService _service = Substitute.For<IStreamQueryService>();
    private readonly AdminTracesController _sut;

    public AdminTracesControllerTests() => _sut = new AdminTracesController(_service, NullLogger<AdminTracesController>.Instance) {
        ControllerContext = new ControllerContext {
            HttpContext = new DefaultHttpContext {
                User = CreatePrincipal("ReadOnly", "tenant-a"),
            },
        }
    };

    [Fact]
    public async Task GetCorrelationTraceMap_ReturnsOk_WhenTraceMapAvailable() {
        var expected = new CorrelationTraceMap(
            "corr-1", "t1", "d1", "a1",
            "Cmd", "Completed", "user-1",
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 100,
            [], [], null, null, null, 10, false, null);

        _ = _service.GetCorrelationTraceMapAsync("t1", "corr-1", null, null, Arg.Any<CancellationToken>())
            .Returns(expected);

        IActionResult result = await _sut.GetCorrelationTraceMap("t1", "corr-1", null, null);

        OkObjectResult okResult = result.ShouldBeOfType<OkObjectResult>();
        okResult.Value.ShouldBe(expected);
    }

    [Fact]
    public async Task GetCorrelationTraceMap_EmptyCorrelationId_Returns400() {
        IActionResult result = await _sut.GetCorrelationTraceMap("t1", "", null, null);

        ObjectResult objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task GetCorrelationTraceMap_WhitespaceCorrelationId_Returns400() {
        IActionResult result = await _sut.GetCorrelationTraceMap("t1", "   ", null, null);

        ObjectResult objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task GetCorrelationTraceMap_ServiceUnavailable_Returns503() {
        _ = _service.GetCorrelationTraceMapAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Throws(new Grpc.Core.RpcException(new Grpc.Core.Status(Grpc.Core.StatusCode.Unavailable, "test")));

        IActionResult result = await _sut.GetCorrelationTraceMap("t1", "corr-1", null, null);

        ObjectResult objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(StatusCodes.Status503ServiceUnavailable);
    }

    [Fact]
    public async Task GetCorrelationTraceMap_UnexpectedException_Returns500() {
        _ = _service.GetCorrelationTraceMapAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("unexpected"));

        IActionResult result = await _sut.GetCorrelationTraceMap("t1", "corr-1", null, null);

        ObjectResult objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(StatusCodes.Status500InternalServerError);
        ProblemDetails problemDetails = objectResult.Value.ShouldBeOfType<ProblemDetails>();
        problemDetails.Extensions.ShouldContainKey("correlationId");
    }

    [Fact]
    public async Task GetCorrelationTraceMap_PassesDomainAndAggregateId() {
        var expected = new CorrelationTraceMap(
            "corr-1", "t1", "d1", "a1",
            "Cmd", "Completed", null,
            null, null, null,
            [], [], null, null, null, 0, false, null);

        _ = _service.GetCorrelationTraceMapAsync("t1", "corr-1", "d1", "a1", Arg.Any<CancellationToken>())
            .Returns(expected);

        IActionResult result = await _sut.GetCorrelationTraceMap("t1", "corr-1", "d1", "a1");
        _ = result.ShouldBeOfType<OkObjectResult>();
        _ = await _service.Received(1).GetCorrelationTraceMapAsync("t1", "corr-1", "d1", "a1", Arg.Any<CancellationToken>());
    }

    private static ClaimsPrincipal CreatePrincipal(string adminRole, params string[] tenants) {
        var claims = new List<Claim> { new(AdminClaimTypes.AdminRole, adminRole) };
        foreach (string tenant in tenants) {
            claims.Add(new Claim(AdminClaimTypes.Tenant, tenant));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }
}
