using System.Net;
using System.Security.Claims;

using Hexalith.EventStore.Admin.Abstractions.Models.Tenants;
using Hexalith.EventStore.Admin.Abstractions.Services;
using Hexalith.EventStore.Admin.Server.Authorization;
using Hexalith.EventStore.Admin.Server.Controllers;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

namespace Hexalith.EventStore.Admin.Server.Tests.Controllers;

public class AdminTenantsControllerTests {
    private readonly ITenantQueryService _service = Substitute.For<ITenantQueryService>();
    private readonly ITenantCommandService _commandService = Substitute.For<ITenantCommandService>();
    private readonly AdminTenantsController _sut;

    public AdminTenantsControllerTests() => _sut = new AdminTenantsController(_service, _commandService, NullLogger<AdminTenantsController>.Instance) {
        ControllerContext = new ControllerContext {
            HttpContext = new DefaultHttpContext {
                User = CreatePrincipal("Admin"),
            },
        }
    };

    [Fact]
    public async Task ListTenants_DelegatesToService() {
        IReadOnlyList<TenantSummary> expected = [];
        _ = _service.ListTenantsAsync(Arg.Any<CancellationToken>())
            .Returns(expected);

        IActionResult result = await _sut.ListTenants();

        OkObjectResult okResult = result.ShouldBeOfType<OkObjectResult>();
        okResult.Value.ShouldBe(expected);
    }

    [Fact]
    public async Task GetTenantDetail_ReturnsForbidden_WhenQueryPipelineRejectsRequest() {
        _ = _service.GetTenantDetailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException<TenantDetail?>(new HttpRequestException("Forbidden", null, HttpStatusCode.Forbidden)));

        IActionResult result = await _sut.GetTenantDetail("tenant-a");

        ObjectResult objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task GetTenantUsers_ReturnsNotFound_WhenQueryPipelineReportsMissingTenant() {
        _ = _service.GetTenantUsersAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException<IReadOnlyList<TenantUser>>(new HttpRequestException("Not Found", null, HttpStatusCode.NotFound)));

        IActionResult result = await _sut.GetTenantUsers("tenant-a");

        ObjectResult objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
    }

    private static ClaimsPrincipal CreatePrincipal(string adminRole) {
        var claims = new List<Claim> { new(AdminClaimTypes.AdminRole, adminRole) };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }
}
