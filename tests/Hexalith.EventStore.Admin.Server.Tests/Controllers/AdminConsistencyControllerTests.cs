using System.Security.Claims;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Models.Consistency;
using Hexalith.EventStore.Admin.Abstractions.Services;
using Hexalith.EventStore.Admin.Server.Authorization;
using Hexalith.EventStore.Admin.Server.Controllers;
using Hexalith.EventStore.Admin.Server.Models;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

namespace Hexalith.EventStore.Admin.Server.Tests.Controllers;

public class AdminConsistencyControllerTests {
    private readonly IConsistencyCommandService _commandService = Substitute.For<IConsistencyCommandService>();
    private readonly IConsistencyQueryService _queryService = Substitute.For<IConsistencyQueryService>();
    private readonly AdminConsistencyController _sut;

    public AdminConsistencyControllerTests() => _sut = new AdminConsistencyController(_queryService, _commandService, NullLogger<AdminConsistencyController>.Instance) {
        ControllerContext = new ControllerContext {
            HttpContext = new DefaultHttpContext {
                User = CreatePrincipal("Operator", "tenant-a"),
            },
        }
    };

    [Fact]
    public async Task GetChecks_ReturnsOk_WithCheckList() {
        IReadOnlyList<ConsistencyCheckSummary> expected =
        [
            new ConsistencyCheckSummary(
                "check-1",
                ConsistencyCheckStatus.Completed,
                "tenant-a",
                null,
                [ConsistencyCheckType.SequenceContinuity],
                DateTimeOffset.UtcNow.AddHours(-1),
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.AddMinutes(30),
                50,
                2),
        ];
        _ = _queryService.GetChecksAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        IActionResult result = await _sut.GetChecks("tenant-a");

        OkObjectResult okResult = result.ShouldBeOfType<OkObjectResult>();
        okResult.Value.ShouldBe(expected);
    }

    [Fact]
    public async Task GetChecks_ReturnsOk_WithTenantFilter() {
        IReadOnlyList<ConsistencyCheckSummary> expected = [];
        _ = _queryService.GetChecksAsync("tenant-a", Arg.Any<CancellationToken>())
            .Returns(expected);

        IActionResult result = await _sut.GetChecks("tenant-a");

        OkObjectResult okResult = result.ShouldBeOfType<OkObjectResult>();
        okResult.Value.ShouldBe(expected);
    }

    [Fact]
    public async Task GetCheckResult_ReturnsOk_WhenFound() {
        ConsistencyCheckResult expected = new(
            "check-1",
            ConsistencyCheckStatus.Completed,
            "tenant-a",
            null,
            [ConsistencyCheckType.SequenceContinuity],
            DateTimeOffset.UtcNow.AddHours(-1),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddMinutes(30),
            50,
            1,
            [new ConsistencyAnomaly("anom-1", ConsistencyCheckType.SequenceContinuity, AnomalySeverity.Error, "tenant-a", "orders", "order-1", "Gap at seq 5", null, 5, null)],
            false,
            null);
        _ = _queryService.GetCheckResultAsync("check-1", Arg.Any<CancellationToken>())
            .Returns(expected);

        IActionResult result = await _sut.GetCheckResult("check-1");

        OkObjectResult okResult = result.ShouldBeOfType<OkObjectResult>();
        okResult.Value.ShouldBe(expected);
    }

    [Fact]
    public async Task GetCheckResult_ReturnsNotFound_WhenMissing() {
        _ = _queryService.GetCheckResultAsync("check-999", Arg.Any<CancellationToken>())
            .Returns((ConsistencyCheckResult?)null);

        IActionResult result = await _sut.GetCheckResult("check-999");

        ObjectResult objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task TriggerCheck_Returns202_ForOperator() {
        var expected = new AdminOperationResult(true, "check-new", "Consistency check started.", null);
        _ = _commandService.TriggerCheckAsync(
                "tenant-a",
                null,
                Arg.Any<IReadOnlyList<ConsistencyCheckType>>(),
                Arg.Any<CancellationToken>())
            .Returns(expected);

        var request = new ConsistencyCheckRequest("tenant-a", null, [ConsistencyCheckType.SequenceContinuity]);
        IActionResult result = await _sut.TriggerCheck(request);

        _ = result.ShouldBeOfType<AcceptedResult>();
    }

    [Fact]
    public async Task TriggerCheck_Returns403_WhenTenantInBodyDoesNotMatchClaim() {
        var request = new ConsistencyCheckRequest("tenant-b", null, [ConsistencyCheckType.SequenceContinuity]);
        IActionResult result = await _sut.TriggerCheck(request);

        ObjectResult objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task TriggerCheck_Returns409_WhenCheckAlreadyRunning() {
        var conflict = new AdminOperationResult(false, "check-x", "A check is already active for this tenant.", "Conflict");
        _ = _commandService.TriggerCheckAsync(
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<IReadOnlyList<ConsistencyCheckType>>(),
                Arg.Any<CancellationToken>())
            .Returns(conflict);

        var request = new ConsistencyCheckRequest("tenant-a", null, [ConsistencyCheckType.SequenceContinuity]);
        IActionResult result = await _sut.TriggerCheck(request);

        ObjectResult objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task CancelCheck_Returns200_WhenRunning() {
        var expected = new AdminOperationResult(true, "check-1", "Consistency check cancelled.", null);
        _ = _commandService.CancelCheckAsync("check-1", Arg.Any<CancellationToken>())
            .Returns(expected);

        IActionResult result = await _sut.CancelCheck("check-1");

        OkObjectResult okResult = result.ShouldBeOfType<OkObjectResult>();
        okResult.Value.ShouldBe(expected);
    }

    [Fact]
    public async Task CancelCheck_Returns422_WhenNotRunning() {
        var invalid = new AdminOperationResult(false, "check-1", "Cannot cancel a check with status 'Completed'.", "InvalidOperation");
        _ = _commandService.CancelCheckAsync("check-1", Arg.Any<CancellationToken>())
            .Returns(invalid);

        IActionResult result = await _sut.CancelCheck("check-1");

        ObjectResult objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(StatusCodes.Status422UnprocessableEntity);
    }

    private static ClaimsPrincipal CreatePrincipal(string adminRole, params string[] tenants) {
        List<Claim> claims = [new(AdminClaimTypes.AdminRole, adminRole)];
        foreach (string tenant in tenants) {
            claims.Add(new Claim(AdminClaimTypes.Tenant, tenant));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }
}
