using System.Security.Claims;

using Hexalith.EventStore.Admin.Abstractions.Models.Tenants;
using Hexalith.EventStore.Admin.Abstractions.Services;
using Hexalith.EventStore.Admin.Server.Authorization;
using Hexalith.EventStore.Admin.Server.Controllers;
using Hexalith.EventStore.Admin.Server.Models;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

namespace Hexalith.EventStore.Admin.Server.Tests.Controllers;

public class AdminTenantsControllerTests
{
    private readonly ITenantQueryService _service = Substitute.For<ITenantQueryService>();
    private readonly ITenantCommandService _commandService = Substitute.For<ITenantCommandService>();
    private readonly AdminTenantsController _sut;

    public AdminTenantsControllerTests()
    {
        _sut = new AdminTenantsController(_service, _commandService, NullLogger<AdminTenantsController>.Instance);
        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = CreatePrincipal("Admin"),
            },
        };
    }

    [Fact]
    public async Task ListTenants_DelegatesToService()
    {
        IReadOnlyList<TenantSummary> expected = [];
        _service.ListTenantsAsync(Arg.Any<CancellationToken>())
            .Returns(expected);

        IActionResult result = await _sut.ListTenants();

        var okResult = result.ShouldBeOfType<OkObjectResult>();
        okResult.Value.ShouldBe(expected);
    }

    [Fact]
    public async Task GetTenantQuotas_NullResult_Returns404()
    {
        _service.GetTenantQuotasAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((TenantQuotas)null!);

        IActionResult result = await _sut.GetTenantQuotas("tenant-a");

        var objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task CompareTenantUsage_DelegatesToService()
    {
        var comparison = new TenantComparison([], DateTimeOffset.UtcNow);
        _service.CompareTenantUsageAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(comparison);

        IActionResult result = await _sut.CompareTenantUsage(new TenantCompareRequest(["t1", "t2"]));

        var okResult = result.ShouldBeOfType<OkObjectResult>();
        okResult.Value.ShouldBe(comparison);
    }

    private static ClaimsPrincipal CreatePrincipal(string adminRole)
    {
        var claims = new List<Claim> { new(AdminClaimTypes.AdminRole, adminRole) };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }
}
