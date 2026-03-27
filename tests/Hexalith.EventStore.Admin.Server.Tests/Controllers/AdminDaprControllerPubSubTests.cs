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

using Shouldly;

namespace Hexalith.EventStore.Admin.Server.Tests.Controllers;

public class AdminDaprControllerPubSubTests
{
    private readonly IDaprInfrastructureQueryService _service = Substitute.For<IDaprInfrastructureQueryService>();
    private readonly AdminDaprController _sut;

    public AdminDaprControllerPubSubTests()
    {
        _sut = new AdminDaprController(_service, NullLogger<AdminDaprController>.Instance);
        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = CreatePrincipal("ReadOnly"),
            },
        };
    }

    [Fact]
    public async Task GetPubSubOverview_Returns200_WithFullData()
    {
        DaprPubSubOverview expected = new(
            [new DaprComponentDetail("pubsub", "pubsub.redis", DaprComponentCategory.PubSub, "v1", HealthStatus.Healthy, DateTimeOffset.UtcNow, [])],
            [new DaprSubscriptionInfo("pubsub", "*.*.events", "/events/handle", "DECLARATIVE", null)],
            true);
        _service.GetPubSubOverviewAsync(Arg.Any<CancellationToken>()).Returns(expected);

        IActionResult result = await _sut.GetPubSubOverviewAsync();

        OkObjectResult okResult = result.ShouldBeOfType<OkObjectResult>();
        okResult.Value.ShouldBe(expected);
    }

    [Fact]
    public async Task GetPubSubOverview_Returns200_WithPartialData()
    {
        DaprPubSubOverview expected = new(
            [new DaprComponentDetail("pubsub", "pubsub.redis", DaprComponentCategory.PubSub, "v1", HealthStatus.Healthy, DateTimeOffset.UtcNow, [])],
            [],
            false);
        _service.GetPubSubOverviewAsync(Arg.Any<CancellationToken>()).Returns(expected);

        IActionResult result = await _sut.GetPubSubOverviewAsync();

        OkObjectResult okResult = result.ShouldBeOfType<OkObjectResult>();
        DaprPubSubOverview overview = okResult.Value.ShouldBeOfType<DaprPubSubOverview>();
        overview.IsRemoteMetadataAvailable.ShouldBeFalse();
    }

    [Fact]
    public async Task GetPubSubOverview_Returns503_WhenServiceUnavailable()
    {
        _service.GetPubSubOverviewAsync(Arg.Any<CancellationToken>())
            .Returns<DaprPubSubOverview>(_ => throw new HttpRequestException("Unavailable"));

        IActionResult result = await _sut.GetPubSubOverviewAsync();

        ObjectResult objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(503);
    }

    [Fact]
    public async Task GetPubSubOverview_Returns500_WhenUnexpectedError()
    {
        _service.GetPubSubOverviewAsync(Arg.Any<CancellationToken>())
            .Returns<DaprPubSubOverview>(_ => throw new InvalidOperationException("Unexpected"));

        IActionResult result = await _sut.GetPubSubOverviewAsync();

        ObjectResult objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(500);
    }

    private static ClaimsPrincipal CreatePrincipal(string adminRole)
    {
        List<Claim> claims = [new(AdminClaimTypes.AdminRole, adminRole)];
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }
}
