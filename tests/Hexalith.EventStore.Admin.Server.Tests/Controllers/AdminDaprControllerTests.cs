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

public class AdminDaprControllerTests
{
    private readonly IDaprInfrastructureQueryService _service = Substitute.For<IDaprInfrastructureQueryService>();
    private readonly AdminDaprController _sut;

    public AdminDaprControllerTests()
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
    public async Task GetComponents_DelegatesToService()
    {
        IReadOnlyList<DaprComponentDetail> expected =
        [
            new DaprComponentDetail("statestore", "state.redis", DaprComponentCategory.StateStore, "v1", HealthStatus.Healthy, DateTimeOffset.UtcNow, []),
        ];
        _service.GetComponentsAsync(Arg.Any<CancellationToken>())
            .Returns(expected);

        IActionResult result = await _sut.GetComponents();

        OkObjectResult okResult = result.ShouldBeOfType<OkObjectResult>();
        okResult.Value.ShouldBe(expected);
    }

    [Fact]
    public async Task GetSidecar_DelegatesToService()
    {
        DaprSidecarInfo expected = new("test-app", "1.14.0", 3, 2, 1, RemoteMetadataStatus.Available, "http://localhost:3501");
        _service.GetSidecarInfoAsync(Arg.Any<CancellationToken>())
            .Returns(expected);

        IActionResult result = await _sut.GetSidecar();

        OkObjectResult okResult = result.ShouldBeOfType<OkObjectResult>();
        okResult.Value.ShouldBe(expected);
    }

    [Fact]
    public async Task GetComponents_Returns503_WhenServiceUnavailable()
    {
        _service.GetComponentsAsync(Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<DaprComponentDetail>>(_ => throw new HttpRequestException("Unavailable"));

        IActionResult result = await _sut.GetComponents();

        ObjectResult objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(503);
    }

    [Fact]
    public async Task GetSidecar_Returns503_WhenServiceUnavailable()
    {
        _service.GetSidecarInfoAsync(Arg.Any<CancellationToken>())
            .Returns<DaprSidecarInfo?>(_ => throw new HttpRequestException("Unavailable"));

        IActionResult result = await _sut.GetSidecar();

        ObjectResult objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(503);
    }

    [Fact]
    public async Task GetSidecar_Returns404_WhenSidecarUnavailable()
    {
        _service.GetSidecarInfoAsync(Arg.Any<CancellationToken>())
            .Returns((DaprSidecarInfo?)null);

        IActionResult result = await _sut.GetSidecar();

        result.ShouldBeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetComponents_Returns500_WhenUnexpectedError()
    {
        _service.GetComponentsAsync(Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<DaprComponentDetail>>(_ => throw new InvalidOperationException("Unexpected"));

        IActionResult result = await _sut.GetComponents();

        ObjectResult objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(500);
    }

    private static ClaimsPrincipal CreatePrincipal(string adminRole)
    {
        List<Claim> claims = [new(AdminClaimTypes.AdminRole, adminRole)];
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }
}
