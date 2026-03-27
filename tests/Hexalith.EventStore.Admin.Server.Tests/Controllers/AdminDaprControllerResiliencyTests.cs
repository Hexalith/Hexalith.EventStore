using System.Security.Claims;

using Hexalith.EventStore.Admin.Abstractions.Models.Dapr;
using Hexalith.EventStore.Admin.Abstractions.Services;
using Hexalith.EventStore.Admin.Server.Authorization;
using Hexalith.EventStore.Admin.Server.Controllers;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Admin.Server.Tests.Controllers;

public class AdminDaprControllerResiliencyTests
{
    private readonly IDaprInfrastructureQueryService _service = Substitute.For<IDaprInfrastructureQueryService>();
    private readonly AdminDaprController _sut;

    public AdminDaprControllerResiliencyTests()
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
    public async Task GetResiliencySpec_Returns200_WithFullSpec()
    {
        DaprResiliencySpec expected = new(
            [new DaprRetryPolicy("defaultRetry", "exponential", 10, null, "15s")],
            [new DaprTimeoutPolicy("daprSidecar", "5s")],
            [new DaprCircuitBreakerPolicy("defaultBreaker", 1, "60s", "60s", "consecutiveFailures > 5")],
            [new DaprResiliencyTargetBinding("commandapi", "App", null, "defaultRetry", "daprSidecar", "defaultBreaker")],
            IsConfigurationAvailable: true,
            RawYamlContent: "spec: ...",
            ErrorMessage: null);
        _service.GetResiliencySpecAsync(Arg.Any<CancellationToken>()).Returns(expected);

        IActionResult result = await _sut.GetResiliencySpecAsync();

        OkObjectResult okResult = result.ShouldBeOfType<OkObjectResult>();
        okResult.Value.ShouldBe(expected);
    }

    [Fact]
    public async Task GetResiliencySpec_Returns200_WithUnavailableConfig()
    {
        DaprResiliencySpec unavailable = DaprResiliencySpec.Unavailable;
        _service.GetResiliencySpecAsync(Arg.Any<CancellationToken>()).Returns(unavailable);

        IActionResult result = await _sut.GetResiliencySpecAsync();

        OkObjectResult okResult = result.ShouldBeOfType<OkObjectResult>();
        DaprResiliencySpec spec = okResult.Value.ShouldBeOfType<DaprResiliencySpec>();
        spec.IsConfigurationAvailable.ShouldBeFalse();
        spec.RetryPolicies.ShouldBeEmpty();
        spec.TimeoutPolicies.ShouldBeEmpty();
        spec.CircuitBreakerPolicies.ShouldBeEmpty();
        spec.TargetBindings.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetResiliencySpec_Returns500_OnUnexpectedException()
    {
        _service.GetResiliencySpecAsync(Arg.Any<CancellationToken>())
            .Returns<DaprResiliencySpec>(_ => throw new InvalidOperationException("Unexpected"));

        IActionResult result = await _sut.GetResiliencySpecAsync();

        ObjectResult objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(500);
    }

    private static ClaimsPrincipal CreatePrincipal(string adminRole)
    {
        List<Claim> claims = [new(AdminClaimTypes.AdminRole, adminRole)];
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }
}
