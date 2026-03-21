using System.Security.Claims;

using Hexalith.EventStore.Admin.Abstractions.Models.Health;
using Hexalith.EventStore.Admin.Abstractions.Services;
using Hexalith.EventStore.Admin.Server.Authorization;
using Hexalith.EventStore.Admin.Server.Controllers;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

namespace Hexalith.EventStore.Admin.Server.Tests.Controllers;

public class AdminHealthControllerTests
{
    private readonly IHealthQueryService _service = Substitute.For<IHealthQueryService>();
    private readonly AdminHealthController _sut;

    public AdminHealthControllerTests()
    {
        _sut = new AdminHealthController(_service, NullLogger<AdminHealthController>.Instance);
        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = CreatePrincipal("ReadOnly"),
            },
        };
    }

    [Fact]
    public async Task GetSystemHealth_DelegatesToService()
    {
        var expected = new SystemHealthReport(HealthStatus.Healthy, 0, 0.0, 0.0, [], new ObservabilityLinks(null, null, null));
        _service.GetSystemHealthAsync(Arg.Any<CancellationToken>())
            .Returns(expected);

        IActionResult result = await _sut.GetSystemHealth();

        var okResult = result.ShouldBeOfType<OkObjectResult>();
        okResult.Value.ShouldBe(expected);
    }

    [Fact]
    public async Task GetDaprComponentStatus_DelegatesToService()
    {
        IReadOnlyList<DaprComponentHealth> expected = [];
        _service.GetDaprComponentStatusAsync(Arg.Any<CancellationToken>())
            .Returns(expected);

        IActionResult result = await _sut.GetDaprComponentStatus();

        var okResult = result.ShouldBeOfType<OkObjectResult>();
        okResult.Value.ShouldBe(expected);
    }

    private static ClaimsPrincipal CreatePrincipal(string adminRole)
    {
        var claims = new List<Claim> { new(AdminClaimTypes.AdminRole, adminRole) };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }
}
