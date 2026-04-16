using System.Security.Claims;

using Hexalith.EventStore.Admin.Abstractions.Models.Dapr;
using Hexalith.EventStore.Admin.Abstractions.Models.Health;
using Hexalith.EventStore.Admin.Abstractions.Services;
using Hexalith.EventStore.Admin.Server.Authorization;
using Hexalith.EventStore.Admin.Server.Controllers;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

namespace Hexalith.EventStore.Admin.Server.Tests.Controllers;

public class AdminHealthControllerHistoryTests {
    private readonly IHealthQueryService _service = Substitute.For<IHealthQueryService>();
    private readonly AdminHealthController _sut;

    public AdminHealthControllerHistoryTests() => _sut = new AdminHealthController(_service, NullLogger<AdminHealthController>.Instance) {
        ControllerContext = new ControllerContext {
            HttpContext = new DefaultHttpContext {
                User = CreatePrincipal("ReadOnly"),
            },
        }
    };

    [Fact]
    public async Task GetComponentHealthHistory_Returns200_WithTimelineData() {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var expected = new DaprComponentHealthTimeline(
            [new DaprHealthHistoryEntry("statestore", "state.redis", HealthStatus.Healthy, now)],
            HasData: true);

        _ = _service.GetComponentHealthHistoryAsync(
            Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        IActionResult result = await _sut.GetComponentHealthHistoryAsync(
            from: now.AddHours(-24), to: now);

        OkObjectResult okResult = result.ShouldBeOfType<OkObjectResult>();
        okResult.Value.ShouldBe(expected);
    }

    [Fact]
    public async Task GetComponentHealthHistory_Returns400_WhenRangeExceeds7Days() {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        IActionResult result = await _sut.GetComponentHealthHistoryAsync(
            from: now.AddDays(-8), to: now);

        BadRequestObjectResult badRequest = result.ShouldBeOfType<BadRequestObjectResult>();
        ProblemDetails problemDetails = badRequest.Value.ShouldBeOfType<ProblemDetails>();
        problemDetails.Title.ShouldBe("Time range too large");
    }

    [Fact]
    public async Task GetComponentHealthHistory_Returns200_WhenRangeIsExactly7Days() {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DaprComponentHealthTimeline expected = DaprComponentHealthTimeline.Empty;

        _ = _service.GetComponentHealthHistoryAsync(
            Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        IActionResult result = await _sut.GetComponentHealthHistoryAsync(
            from: now.AddDays(-7), to: now);

        _ = result.ShouldBeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetComponentHealthHistory_DefaultsToLast24Hours_WhenNoParametersProvided() {
        DaprComponentHealthTimeline expected = DaprComponentHealthTimeline.Empty;
        _ = _service.GetComponentHealthHistoryAsync(
            Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        IActionResult result = await _sut.GetComponentHealthHistoryAsync();

        _ = result.ShouldBeOfType<OkObjectResult>();

        // Verify service was called with approximately 24h range
        _ = await _service.Received(1).GetComponentHealthHistoryAsync(
            Arg.Is<DateTimeOffset>(d => (DateTimeOffset.UtcNow - d).TotalHours > 23.5 && (DateTimeOffset.UtcNow - d).TotalHours < 24.5),
            Arg.Is<DateTimeOffset>(d => (DateTimeOffset.UtcNow - d).TotalMinutes < 1),
            null,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetComponentHealthHistory_PassesComponentFilter() {
        DaprComponentHealthTimeline expected = DaprComponentHealthTimeline.Empty;
        _ = _service.GetComponentHealthHistoryAsync(
            Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        IActionResult result = await _sut.GetComponentHealthHistoryAsync(
            component: "statestore");

        _ = result.ShouldBeOfType<OkObjectResult>();

        _ = await _service.Received(1).GetComponentHealthHistoryAsync(
            Arg.Any<DateTimeOffset>(),
            Arg.Any<DateTimeOffset>(),
            "statestore",
            Arg.Any<CancellationToken>());
    }

    private static ClaimsPrincipal CreatePrincipal(string adminRole) {
        var claims = new List<Claim> { new(AdminClaimTypes.AdminRole, adminRole) };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }
}
