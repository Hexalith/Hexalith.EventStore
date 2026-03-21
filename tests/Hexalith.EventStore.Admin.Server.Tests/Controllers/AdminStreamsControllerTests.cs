using System.Security.Claims;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
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

public class AdminStreamsControllerTests
{
    private readonly IStreamQueryService _service = Substitute.For<IStreamQueryService>();
    private readonly AdminStreamsController _sut;

    public AdminStreamsControllerTests()
    {
        _sut = new AdminStreamsController(_service, NullLogger<AdminStreamsController>.Instance);
        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = CreatePrincipal("ReadOnly", "tenant-a"),
            },
        };
    }

    [Fact]
    public async Task GetRecentlyActiveStreams_ReturnsOk()
    {
        var expected = new PagedResult<StreamSummary>([], 0, null);
        _service.GetRecentlyActiveStreamsAsync(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        IActionResult result = await _sut.GetRecentlyActiveStreams("tenant-a", null, 100);

        var okResult = result.ShouldBeOfType<OkObjectResult>();
        okResult.Value.ShouldBe(expected);
    }

    [Fact]
    public async Task GetAggregateState_NullResult_Returns404()
    {
        _service.GetAggregateStateAtPositionAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns((AggregateStateSnapshot)null!);

        IActionResult result = await _sut.GetAggregateState("t", "d", "a", 1);

        var objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task GetRecentlyActiveStreams_ServiceThrowsRpcException_Returns503()
    {
        _service.GetRecentlyActiveStreamsAsync(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Throws(new Grpc.Core.RpcException(new Grpc.Core.Status(Grpc.Core.StatusCode.Unavailable, "test")));

        IActionResult result = await _sut.GetRecentlyActiveStreams("tenant-a", null, 100);

        var objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(StatusCodes.Status503ServiceUnavailable);
    }

    [Fact]
    public async Task GetRecentlyActiveStreams_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        var expected = new PagedResult<StreamSummary>([], 0, null);
        _service.GetRecentlyActiveStreamsAsync(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<int>(), cts.Token)
            .Returns(expected);

        await _sut.GetRecentlyActiveStreams("tenant-a", null, 100, cts.Token);

        await _service.Received(1).GetRecentlyActiveStreamsAsync(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<int>(), cts.Token);
    }

    [Fact]
    public async Task GetAggregateState_NullResult_Returns404WithCorrelationId()
    {
        _service.GetAggregateStateAtPositionAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns((AggregateStateSnapshot)null!);

        IActionResult result = await _sut.GetAggregateState("t", "d", "a", 1);

        var objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
        var problemDetails = objectResult.Value.ShouldBeOfType<ProblemDetails>();
        problemDetails.Extensions.ShouldContainKey("correlationId");
    }

    [Fact]
    public async Task GetRecentlyActiveStreams_ServiceThrowsRpcException_Returns503WithCorrelationId()
    {
        _service.GetRecentlyActiveStreamsAsync(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Throws(new Grpc.Core.RpcException(new Grpc.Core.Status(Grpc.Core.StatusCode.Unavailable, "test")));

        IActionResult result = await _sut.GetRecentlyActiveStreams("tenant-a", null, 100);

        var objectResult = result.ShouldBeOfType<ObjectResult>();
        var problemDetails = objectResult.Value.ShouldBeOfType<ProblemDetails>();
        problemDetails.Extensions.ShouldContainKey("correlationId");
    }

    [Fact]
    public async Task GetRecentlyActiveStreams_UnexpectedException_Returns500WithCorrelationId()
    {
        _service.GetRecentlyActiveStreamsAsync(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("unexpected"));

        IActionResult result = await _sut.GetRecentlyActiveStreams("tenant-a", null, 100);

        var objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(StatusCodes.Status500InternalServerError);
        var problemDetails = objectResult.Value.ShouldBeOfType<ProblemDetails>();
        problemDetails.Extensions.ShouldContainKey("correlationId");
        problemDetails.Detail.ShouldBe("An unexpected error occurred."); // generic message, no exception details leaked
    }

    private static ClaimsPrincipal CreatePrincipal(string adminRole, params string[] tenants)
    {
        var claims = new List<Claim> { new(AdminClaimTypes.AdminRole, adminRole) };
        foreach (string tenant in tenants)
        {
            claims.Add(new Claim(AdminClaimTypes.Tenant, tenant));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }
}
