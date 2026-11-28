using System.Security.Claims;
using System.Reflection;

using Hexalith.EventStore.Admin.Abstractions.Models.DeadLetters;
using Hexalith.EventStore.Admin.Abstractions.Services;
using Hexalith.EventStore.Admin.Server.Authorization;
using Hexalith.EventStore.Admin.Server.Controllers;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

namespace Hexalith.EventStore.Admin.Server.Tests.Controllers;

public class AdminDeadLettersControllerCountTests {
    private readonly IDeadLetterQueryService _queryService = Substitute.For<IDeadLetterQueryService>();
    private readonly IDeadLetterCommandService _commandService = Substitute.For<IDeadLetterCommandService>();
    private readonly AdminDeadLettersController _sut;

    public AdminDeadLettersControllerCountTests() => _sut = new AdminDeadLettersController(_queryService, _commandService, NullLogger<AdminDeadLettersController>.Instance) {
        ControllerContext = new ControllerContext {
            HttpContext = new DefaultHttpContext {
                User = CreatePrincipal("Admin"),
            },
        }
    };

    [Fact]
    public async Task GetDeadLetterCount_Returns200_WithCount() {
        _ = _queryService.GetDeadLetterCountAsync(Arg.Any<CancellationToken>()).Returns(42);

        IActionResult result = await _sut.GetDeadLetterCount();

        OkObjectResult okResult = result.ShouldBeOfType<OkObjectResult>();
        okResult.Value.ShouldBe(42);
    }

    [Fact]
    public async Task GetDeadLetterCount_Returns200_WithZero() {
        _ = _queryService.GetDeadLetterCountAsync(Arg.Any<CancellationToken>()).Returns(0);

        IActionResult result = await _sut.GetDeadLetterCount();

        OkObjectResult okResult = result.ShouldBeOfType<OkObjectResult>();
        okResult.Value.ShouldBe(0);
    }

    [Fact]
    public async Task GetDeadLetterCount_Returns503_WhenServiceUnavailable() {
        _ = _queryService.GetDeadLetterCountAsync(Arg.Any<CancellationToken>())
            .Returns<int>(_ => throw new HttpRequestException("Unavailable"));

        IActionResult result = await _sut.GetDeadLetterCount();

        ObjectResult objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(503);
    }

    [Fact]
    public async Task GetDeadLetterCount_Returns500_WhenUnexpectedError() {
        _ = _queryService.GetDeadLetterCountAsync(Arg.Any<CancellationToken>())
            .Returns<int>(_ => throw new InvalidOperationException("Unexpected"));

        IActionResult result = await _sut.GetDeadLetterCount();

        ObjectResult objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(500);
    }

    [Fact]
    public async Task GetDeadLetterCount_RequiresReadOnlyPolicyAndDeniesUnscopedNonAdmin() {
        AuthorizeAttribute attribute = typeof(AdminDeadLettersController)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Single(method => method.Name == nameof(AdminDeadLettersController.GetDeadLetterCount)
                && method.GetCustomAttribute<HttpGetAttribute>() is not null)
            .GetCustomAttributes<AuthorizeAttribute>()
            .ShouldHaveSingleItem();

        attribute.Policy.ShouldBe(AdminAuthorizationPolicies.ReadOnly);
        _sut.ControllerContext.HttpContext.User = CreatePrincipal("ReadOnly");
        (await _sut.GetDeadLetterCount()).ShouldBeOfType<ObjectResult>().StatusCode.ShouldBe(403);
        _ = await _queryService.DidNotReceiveWithAnyArgs().GetDeadLetterCountAsync(default);
    }

    [Fact]
    public async Task GetDeadLetterCount_DetailedReturnsAuthorizedGlobalSummary() {
        var summary = new DeadLetterSummary(4, TimeSpan.FromMinutes(12));
        _ = _queryService.GetDeadLetterSummaryAsync(Arg.Is((string?)null), Arg.Any<CancellationToken>()).Returns(summary);

        IActionResult result = await _sut.GetDeadLetterCount(detailed: true);

        result.ShouldBeOfType<OkObjectResult>().Value.ShouldBe(summary);
        _ = await _queryService.DidNotReceiveWithAnyArgs().GetDeadLetterCountAsync(default);
    }

    [Fact]
    public async Task GetDeadLetterCount_ScopesSingleTenantReaderToItsGrant() {
        _sut.ControllerContext.HttpContext.User = CreatePrincipal("ReadOnly", "tenant-a");
        _ = _queryService.GetDeadLetterSummaryAsync("tenant-a", Arg.Any<CancellationToken>())
            .Returns(new DeadLetterSummary(2, TimeSpan.FromSeconds(30)));

        IActionResult result = await _sut.GetDeadLetterCount();

        result.ShouldBeOfType<OkObjectResult>().Value.ShouldBe(2);
        _ = await _queryService.DidNotReceiveWithAnyArgs().GetDeadLetterCountAsync(default);
    }

    [Fact]
    public async Task GetDeadLetterCount_DeniesUngrantedTenantBeforeLookup() {
        _sut.ControllerContext.HttpContext.User = CreatePrincipal("ReadOnly", "tenant-a");

        IActionResult result = await _sut.GetDeadLetterCount(tenantId: "tenant-b", detailed: true);

        result.ShouldBeOfType<ObjectResult>().StatusCode.ShouldBe(403);
        _ = await _queryService.DidNotReceiveWithAnyArgs().GetDeadLetterSummaryAsync(default, default);
        _ = await _queryService.DidNotReceiveWithAnyArgs().GetDeadLetterCountAsync(default);
    }

    [Fact]
    public async Task GetDeadLetterCount_ReportsUnavailableSummaryWithoutPartialCount() {
        _ = _queryService.GetDeadLetterSummaryAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns<DeadLetterSummary>(_ => throw new NotSupportedException("Dead-letter summary evidence is unavailable."));

        IActionResult result = await _sut.GetDeadLetterCount(detailed: true);

        ObjectResult problem = result.ShouldBeOfType<ObjectResult>();
        problem.StatusCode.ShouldBe(503);
        problem.Value.ShouldBeOfType<ProblemDetails>().Detail.ShouldNotBeNull().ShouldNotContain("summary evidence");
    }

    private static ClaimsPrincipal CreatePrincipal(string adminRole, params string[] tenants) {
        List<Claim> claims = [new(AdminClaimTypes.AdminRole, adminRole)];
        claims.AddRange(tenants.Select(tenant => new Claim(AdminClaimTypes.Tenant, tenant)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }
}
