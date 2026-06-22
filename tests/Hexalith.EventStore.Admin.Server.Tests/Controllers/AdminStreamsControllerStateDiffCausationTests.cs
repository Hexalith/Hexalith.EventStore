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

/// <summary>
/// Coverage for the state/diff/causation typed-error mapping introduced for the
/// admin-ui-state-inspection-cluster-fix story. Asserts that a missing upstream
/// state/diff/causation surfaces as 404 (not 503), and that an upstream 400 surfaces
/// as 400 (not 503), matching the recently fixed event-detail proxy pattern.
/// </summary>
public class AdminStreamsControllerStateDiffCausationTests {
    private readonly IStreamQueryService _service = Substitute.For<IStreamQueryService>();
    private readonly AdminStreamsController _sut;

    public AdminStreamsControllerStateDiffCausationTests() => _sut = new AdminStreamsController(_service, NullLogger<AdminStreamsController>.Instance) {
        ControllerContext = new ControllerContext {
            HttpContext = new DefaultHttpContext {
                User = CreatePrincipal("ReadOnly", "tenant-a"),
            },
        }
    };

    // -------- /state --------

    [Fact]
    public async Task GetAggregateState_KeyNotFoundException_Returns404NotFound() {
        _ = _service.GetAggregateStateAtPositionAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Throws(new KeyNotFoundException("upstream-not-found"));

        IActionResult result = await _sut.GetAggregateStateAsync("tenant-a", "counter", "agg-1", 5L);

        ObjectResult obj = result.ShouldBeOfType<ObjectResult>();
        obj.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
        ProblemDetails problem = obj.Value.ShouldBeOfType<ProblemDetails>();
        problem.Title.ShouldBe("Not Found");
    }

    [Fact]
    public async Task GetAggregateState_ArgumentException_Returns400BadRequest() {
        _ = _service.GetAggregateStateAtPositionAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Throws(new ArgumentException("invalid-sequence"));

        IActionResult result = await _sut.GetAggregateStateAsync("tenant-a", "counter", "agg-1", -3L);

        ObjectResult obj = result.ShouldBeOfType<ObjectResult>();
        obj.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        ProblemDetails problem = obj.Value.ShouldBeOfType<ProblemDetails>();
        problem.Title.ShouldBe("Bad Request");
        problem.Detail.ShouldBe("invalid-sequence");
    }

    [Fact]
    public async Task GetAggregateState_HttpRequestException_Returns503() {
        _ = _service.GetAggregateStateAtPositionAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Throws(new HttpRequestException("backend down"));

        IActionResult result = await _sut.GetAggregateStateAsync("tenant-a", "counter", "agg-1", 1L);

        ObjectResult obj = result.ShouldBeOfType<ObjectResult>();
        obj.StatusCode.ShouldBe(StatusCodes.Status503ServiceUnavailable);
    }

    [Fact]
    public async Task GetAggregateState_HappyPath_ReturnsOk() {
        AggregateStateSnapshot expected = new("tenant-a", "counter", "agg-1", 5L, DateTimeOffset.UtcNow, "{}");
        _ = _service.GetAggregateStateAtPositionAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        IActionResult result = await _sut.GetAggregateStateAsync("tenant-a", "counter", "agg-1", 5L);

        OkObjectResult ok = result.ShouldBeOfType<OkObjectResult>();
        ok.Value.ShouldBe(expected);
    }

    // -------- /diff --------

    [Fact]
    public async Task DiffAggregateState_KeyNotFoundException_Returns404() {
        _ = _service.DiffAggregateStateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Throws(new KeyNotFoundException("missing diff"));

        IActionResult result = await _sut.DiffAggregateStateAsync("tenant-a", "counter", "agg-1", 1L, 2L);

        ObjectResult obj = result.ShouldBeOfType<ObjectResult>();
        obj.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task DiffAggregateState_ArgumentException_Returns400() {
        _ = _service.DiffAggregateStateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Throws(new ArgumentException("from must be < to"));

        IActionResult result = await _sut.DiffAggregateStateAsync("tenant-a", "counter", "agg-1", 5L, 5L);

        ObjectResult obj = result.ShouldBeOfType<ObjectResult>();
        obj.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task DiffAggregateState_EmptyChangedFields_Returns200() {
        AggregateStateDiff expected = new(1L, 2L, []);
        _ = _service.DiffAggregateStateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        IActionResult result = await _sut.DiffAggregateStateAsync("tenant-a", "counter", "agg-1", 1L, 2L);

        OkObjectResult ok = result.ShouldBeOfType<OkObjectResult>();
        AggregateStateDiff diff = ok.Value.ShouldBeOfType<AggregateStateDiff>();
        diff.ChangedFields.ShouldBeEmpty();
    }

    // -------- /causation --------

    [Fact]
    public async Task TraceCausationChain_KeyNotFoundException_Returns404() {
        _ = _service.TraceCausationChainAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Throws(new KeyNotFoundException("not found"));

        IActionResult result = await _sut.TraceCausationChainAsync("tenant-a", "counter", "agg-1", 1L);

        ObjectResult obj = result.ShouldBeOfType<ObjectResult>();
        obj.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task TraceCausationChain_ArgumentException_Returns400() {
        _ = _service.TraceCausationChainAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Throws(new ArgumentException("at must be >= 1"));

        IActionResult result = await _sut.TraceCausationChainAsync("tenant-a", "counter", "agg-1", 0L);

        ObjectResult obj = result.ShouldBeOfType<ObjectResult>();
        obj.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
    }

    private static ClaimsPrincipal CreatePrincipal(string adminRole, params string[] tenants) {
        var claims = new List<Claim> { new(AdminClaimTypes.AdminRole, adminRole) };
        foreach (string tenant in tenants) {
            claims.Add(new Claim(AdminClaimTypes.Tenant, tenant));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }
}
