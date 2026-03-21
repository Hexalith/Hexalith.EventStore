using System.Security.Claims;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Models.Storage;
using Hexalith.EventStore.Admin.Abstractions.Services;
using Hexalith.EventStore.Admin.Server.Authorization;
using Hexalith.EventStore.Admin.Server.Controllers;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

namespace Hexalith.EventStore.Admin.Server.Tests.Controllers;

public class AdminStorageControllerTests
{
    private readonly IStorageQueryService _queryService = Substitute.For<IStorageQueryService>();
    private readonly IStorageCommandService _commandService = Substitute.For<IStorageCommandService>();
    private readonly AdminStorageController _sut;

    public AdminStorageControllerTests()
    {
        _sut = new AdminStorageController(_queryService, _commandService, NullLogger<AdminStorageController>.Instance);
        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = CreatePrincipal("Operator", "tenant-a"),
            },
        };
    }

    [Fact]
    public async Task GetStorageOverview_DelegatesToQueryService()
    {
        var expected = new StorageOverview(0, 0, []);
        _queryService.GetStorageOverviewAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        IActionResult result = await _sut.GetStorageOverview("tenant-a");

        var okResult = result.ShouldBeOfType<OkObjectResult>();
        okResult.Value.ShouldBe(expected);
    }

    [Fact]
    public async Task TriggerCompaction_DelegatesToCommandService()
    {
        var expected = new AdminOperationResult(true, "op-1", "Compacted", null);
        _commandService.TriggerCompactionAsync("tenant-a", null, Arg.Any<CancellationToken>())
            .Returns(expected);

        IActionResult result = await _sut.TriggerCompaction("tenant-a", null);

        result.ShouldBeOfType<AcceptedResult>();
    }

    [Fact]
    public async Task CreateSnapshot_DelegatesToCommandService()
    {
        var expected = new AdminOperationResult(true, "op-2", "Snapshot created", null);
        _commandService.CreateSnapshotAsync("tenant-a", "domain1", "agg1", Arg.Any<CancellationToken>())
            .Returns(expected);

        IActionResult result = await _sut.CreateSnapshot("tenant-a", "domain1", "agg1");

        var okResult = result.ShouldBeOfType<OkObjectResult>();
        okResult.Value.ShouldBe(expected);
    }

    [Fact]
    public async Task GetHotStreams_DelegatesToQueryService()
    {
        IReadOnlyList<StreamStorageInfo> expected = [];
        _queryService.GetHotStreamsAsync(Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        IActionResult result = await _sut.GetHotStreams("tenant-a", 10);

        var okResult = result.ShouldBeOfType<OkObjectResult>();
        okResult.Value.ShouldBe(expected);
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
