using System.Security.Claims;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Models.Projections;
using Hexalith.EventStore.Admin.Abstractions.Services;
using Hexalith.EventStore.Admin.Server.Authorization;
using Hexalith.EventStore.Admin.Server.Controllers;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

namespace Hexalith.EventStore.Admin.Server.Tests.Controllers;

public class AdminProjectionsControllerTests
{
    private readonly IProjectionQueryService _queryService = Substitute.For<IProjectionQueryService>();
    private readonly IProjectionCommandService _commandService = Substitute.For<IProjectionCommandService>();
    private readonly AdminProjectionsController _sut;

    public AdminProjectionsControllerTests()
    {
        _sut = new AdminProjectionsController(_queryService, _commandService, NullLogger<AdminProjectionsController>.Instance);
        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = CreatePrincipal("Operator", "tenant-a"),
            },
        };
    }

    [Fact]
    public async Task ListProjections_DelegatesToQueryService()
    {
        IReadOnlyList<ProjectionStatus> expected = [];
        _queryService.ListProjectionsAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        IActionResult result = await _sut.ListProjections("tenant-a");

        var okResult = result.ShouldBeOfType<OkObjectResult>();
        okResult.Value.ShouldBe(expected);
    }

    [Fact]
    public async Task PauseProjection_DelegatesToCommandService()
    {
        var expected = new AdminOperationResult(true, "op-1", "Paused", null);
        _commandService.PauseProjectionAsync("tenant-a", "proj1", Arg.Any<CancellationToken>())
            .Returns(expected);

        IActionResult result = await _sut.PauseProjection("tenant-a", "proj1");

        var okResult = result.ShouldBeOfType<OkObjectResult>();
        okResult.Value.ShouldBe(expected);
    }

    [Fact]
    public async Task ResumeProjection_DelegatesToCommandService()
    {
        var expected = new AdminOperationResult(true, "op-2", "Resumed", null);
        _commandService.ResumeProjectionAsync("tenant-a", "proj1", Arg.Any<CancellationToken>())
            .Returns(expected);

        IActionResult result = await _sut.ResumeProjection("tenant-a", "proj1");

        var okResult = result.ShouldBeOfType<OkObjectResult>();
        okResult.Value.ShouldBe(expected);
    }

    [Fact]
    public async Task GetProjectionDetail_NullResult_Returns404()
    {
        _queryService.GetProjectionDetailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((ProjectionDetail)null!);

        IActionResult result = await _sut.GetProjectionDetail("tenant-a", "proj1");

        var objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task PauseProjection_FailedResult_NotFound_Returns404()
    {
        var failedResult = new AdminOperationResult(false, "op-err", "Not found", "NotFound");
        _commandService.PauseProjectionAsync("tenant-a", "proj1", Arg.Any<CancellationToken>())
            .Returns(failedResult);

        IActionResult result = await _sut.PauseProjection("tenant-a", "proj1");

        var objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
        var problemDetails = objectResult.Value.ShouldBeOfType<ProblemDetails>();
        problemDetails.Extensions.ShouldContainKey("correlationId");
    }

    [Fact]
    public async Task PauseProjection_FailedResult_InvalidOperation_Returns422()
    {
        var failedResult = new AdminOperationResult(false, "op-err", "Invalid", "InvalidOperation");
        _commandService.PauseProjectionAsync("tenant-a", "proj1", Arg.Any<CancellationToken>())
            .Returns(failedResult);

        IActionResult result = await _sut.PauseProjection("tenant-a", "proj1");

        var objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(StatusCodes.Status422UnprocessableEntity);
    }

    [Fact]
    public async Task PauseProjection_NullResult_Returns500()
    {
        _commandService.PauseProjectionAsync("tenant-a", "proj1", Arg.Any<CancellationToken>())
            .Returns((AdminOperationResult)null!);

        IActionResult result = await _sut.PauseProjection("tenant-a", "proj1");

        var objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(StatusCodes.Status500InternalServerError);
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
