using System.Security.Claims;

using Hexalith.EventStore.Admin.Abstractions.Models.Dapr;
using Hexalith.EventStore.Admin.Abstractions.Services;
using Hexalith.EventStore.Admin.Server.Authorization;
using Hexalith.EventStore.Admin.Server.Controllers;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

namespace Hexalith.EventStore.Admin.Server.Tests.Controllers;

public class AdminDaprControllerActorTests
{
    private static readonly DaprActorRuntimeConfig _defaultConfig = new(
        TimeSpan.FromMinutes(60),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromSeconds(60),
        true,
        false,
        32);

    private readonly IDaprInfrastructureQueryService _service = Substitute.For<IDaprInfrastructureQueryService>();
    private readonly AdminDaprController _sut;

    public AdminDaprControllerActorTests()
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
    public async Task GetActorRuntimeInfoAsync_DelegatesToService()
    {
        DaprActorRuntimeInfo expected = new(
            [new DaprActorTypeInfo("AggregateActor", 10, "Desc", "format")],
            10,
            _defaultConfig,
            RemoteMetadataStatus.Available,
            "http://localhost:3501");
        _service.GetActorRuntimeInfoAsync(Arg.Any<CancellationToken>())
            .Returns(expected);

        IActionResult result = await _sut.GetActorRuntimeInfoAsync();

        OkObjectResult okResult = result.ShouldBeOfType<OkObjectResult>();
        okResult.Value.ShouldBe(expected);
    }

    [Fact]
    public async Task GetActorRuntimeInfoAsync_Returns503_WhenServiceUnavailable()
    {
        _service.GetActorRuntimeInfoAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Unavailable"));

        IActionResult result = await _sut.GetActorRuntimeInfoAsync();

        ObjectResult objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(503);
    }

    [Fact]
    public async Task GetActorRuntimeInfoAsync_Returns500_WhenUnexpectedError()
    {
        _service.GetActorRuntimeInfoAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Unexpected"));

        IActionResult result = await _sut.GetActorRuntimeInfoAsync();

        ObjectResult objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(500);
    }

    [Fact]
    public async Task GetActorInstanceStateAsync_DelegatesToService()
    {
        DaprActorInstanceState expected = new(
            "ETagActor",
            "Proj:T1",
            [new DaprActorStateEntry("etag", "{}", 2, true)],
            2,
            DateTimeOffset.UtcNow);
        _service.GetActorInstanceStateAsync("ETagActor", "Proj:T1", Arg.Any<CancellationToken>())
            .Returns(expected);

        IActionResult result = await _sut.GetActorInstanceStateAsync("ETagActor", "Proj:T1");

        OkObjectResult okResult = result.ShouldBeOfType<OkObjectResult>();
        okResult.Value.ShouldBe(expected);
    }

    [Fact]
    public async Task GetActorInstanceStateAsync_Returns404_WhenActorTypeUnknown()
    {
        _service.GetActorInstanceStateAsync("Unknown", "id", Arg.Any<CancellationToken>())
            .Returns((DaprActorInstanceState?)null);

        IActionResult result = await _sut.GetActorInstanceStateAsync("Unknown", "id");

        NotFoundObjectResult notFoundResult = result.ShouldBeOfType<NotFoundObjectResult>();
        notFoundResult.StatusCode.ShouldBe(404);
    }

    [Theory]
    [InlineData(null, "id")]
    [InlineData("", "id")]
    [InlineData("  ", "id")]
    [InlineData("type", null)]
    [InlineData("type", "")]
    [InlineData("type", "  ")]
    public async Task GetActorInstanceStateAsync_Returns400_WhenParametersInvalid(string? actorType, string? actorId)
    {
        IActionResult result = await _sut.GetActorInstanceStateAsync(actorType!, actorId!);

        BadRequestObjectResult badResult = result.ShouldBeOfType<BadRequestObjectResult>();
        badResult.StatusCode.ShouldBe(400);
    }

    [Fact]
    public async Task GetActorInstanceStateAsync_Returns503_WhenServiceUnavailable()
    {
        _service.GetActorInstanceStateAsync("ETagActor", "id", Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Unavailable"));

        IActionResult result = await _sut.GetActorInstanceStateAsync("ETagActor", "id");

        ObjectResult objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(503);
    }

    [Fact]
    public async Task GetActorInstanceStateAsync_ColonActorId_RoundTrips()
    {
        string actorId = "tenant1:mydomain:aggregate-123";
        DaprActorInstanceState expected = new(
            "AggregateActor",
            actorId,
            [],
            0,
            DateTimeOffset.UtcNow);
        _service.GetActorInstanceStateAsync("AggregateActor", actorId, Arg.Any<CancellationToken>())
            .Returns(expected);

        IActionResult result = await _sut.GetActorInstanceStateAsync("AggregateActor", actorId);

        OkObjectResult okResult = result.ShouldBeOfType<OkObjectResult>();
        DaprActorInstanceState? state = okResult.Value as DaprActorInstanceState;
        state.ShouldNotBeNull();
        state!.ActorId.ShouldBe(actorId);
    }

    private static ClaimsPrincipal CreatePrincipal(string adminRole)
    {
        List<Claim> claims = [new(AdminClaimTypes.AdminRole, adminRole)];
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }
}
