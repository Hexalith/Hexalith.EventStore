using System.Security.Claims;

using Hexalith.EventStore.Admin.Abstractions.Services;
using Hexalith.EventStore.Admin.Server.Authorization;
using Hexalith.EventStore.Admin.Server.Controllers;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Admin.Server.Tests.Controllers;

public class AdminDeadLettersControllerCountTests
{
    private readonly IDeadLetterQueryService _queryService = Substitute.For<IDeadLetterQueryService>();
    private readonly IDeadLetterCommandService _commandService = Substitute.For<IDeadLetterCommandService>();
    private readonly AdminDeadLettersController _sut;

    public AdminDeadLettersControllerCountTests()
    {
        _sut = new AdminDeadLettersController(_queryService, _commandService, NullLogger<AdminDeadLettersController>.Instance);
        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = CreatePrincipal("ReadOnly"),
            },
        };
    }

    [Fact]
    public async Task GetDeadLetterCount_Returns200_WithCount()
    {
        _queryService.GetDeadLetterCountAsync(Arg.Any<CancellationToken>()).Returns(42);

        IActionResult result = await _sut.GetDeadLetterCount();

        OkObjectResult okResult = result.ShouldBeOfType<OkObjectResult>();
        okResult.Value.ShouldBe(42);
    }

    [Fact]
    public async Task GetDeadLetterCount_Returns200_WithZero()
    {
        _queryService.GetDeadLetterCountAsync(Arg.Any<CancellationToken>()).Returns(0);

        IActionResult result = await _sut.GetDeadLetterCount();

        OkObjectResult okResult = result.ShouldBeOfType<OkObjectResult>();
        okResult.Value.ShouldBe(0);
    }

    [Fact]
    public async Task GetDeadLetterCount_Returns503_WhenServiceUnavailable()
    {
        _queryService.GetDeadLetterCountAsync(Arg.Any<CancellationToken>())
            .Returns<int>(_ => throw new HttpRequestException("Unavailable"));

        IActionResult result = await _sut.GetDeadLetterCount();

        ObjectResult objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(503);
    }

    [Fact]
    public async Task GetDeadLetterCount_Returns500_WhenUnexpectedError()
    {
        _queryService.GetDeadLetterCountAsync(Arg.Any<CancellationToken>())
            .Returns<int>(_ => throw new InvalidOperationException("Unexpected"));

        IActionResult result = await _sut.GetDeadLetterCount();

        ObjectResult objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(500);
    }

    private static ClaimsPrincipal CreatePrincipal(string adminRole)
    {
        List<Claim> claims = [new(AdminClaimTypes.AdminRole, adminRole)];
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }
}
