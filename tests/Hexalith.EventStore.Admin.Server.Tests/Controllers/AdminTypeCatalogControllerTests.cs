using System.Security.Claims;

using Hexalith.EventStore.Admin.Abstractions.Models.TypeCatalog;
using Hexalith.EventStore.Admin.Abstractions.Services;
using Hexalith.EventStore.Admin.Server.Authorization;
using Hexalith.EventStore.Admin.Server.Controllers;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

namespace Hexalith.EventStore.Admin.Server.Tests.Controllers;

public class AdminTypeCatalogControllerTests {
    private readonly ITypeCatalogService _service = Substitute.For<ITypeCatalogService>();
    private readonly AdminTypeCatalogController _sut;

    public AdminTypeCatalogControllerTests() => _sut = new AdminTypeCatalogController(_service, NullLogger<AdminTypeCatalogController>.Instance) {
        ControllerContext = new ControllerContext {
            HttpContext = new DefaultHttpContext {
                User = CreatePrincipal("ReadOnly"),
            },
        }
    };

    [Fact]
    public async Task ListEventTypes_DelegatesToService() {
        IReadOnlyList<EventTypeInfo> expected = [];
        _ = _service.ListEventTypesAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        IActionResult result = await _sut.ListEventTypes("counter");

        OkObjectResult okResult = result.ShouldBeOfType<OkObjectResult>();
        okResult.Value.ShouldBe(expected);
        _ = await _service.Received(1).ListEventTypesAsync("counter", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListCommandTypes_DelegatesToService() {
        IReadOnlyList<CommandTypeInfo> expected = [];
        _ = _service.ListCommandTypesAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        IActionResult result = await _sut.ListCommandTypes(null);

        OkObjectResult okResult = result.ShouldBeOfType<OkObjectResult>();
        okResult.Value.ShouldBe(expected);
        _ = await _service.Received(1).ListCommandTypesAsync(null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListAggregateTypes_DelegatesToService() {
        IReadOnlyList<AggregateTypeInfo> expected = [];
        _ = _service.ListAggregateTypesAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        IActionResult result = await _sut.ListAggregateTypes("orders");

        OkObjectResult okResult = result.ShouldBeOfType<OkObjectResult>();
        okResult.Value.ShouldBe(expected);
        _ = await _service.Received(1).ListAggregateTypesAsync("orders", Arg.Any<CancellationToken>());
    }

    private static ClaimsPrincipal CreatePrincipal(string adminRole) {
        var claims = new List<Claim> { new(AdminClaimTypes.AdminRole, adminRole) };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }
}
