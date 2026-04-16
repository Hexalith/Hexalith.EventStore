using System.Security.Claims;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Models.DeadLetters;
using Hexalith.EventStore.Admin.Abstractions.Services;
using Hexalith.EventStore.Admin.Server.Authorization;
using Hexalith.EventStore.Admin.Server.Controllers;
using Hexalith.EventStore.Admin.Server.Models;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

namespace Hexalith.EventStore.Admin.Server.Tests.Controllers;

public class AdminDeadLettersControllerTests {
    private readonly IDeadLetterQueryService _queryService = Substitute.For<IDeadLetterQueryService>();
    private readonly IDeadLetterCommandService _commandService = Substitute.For<IDeadLetterCommandService>();
    private readonly AdminDeadLettersController _sut;

    public AdminDeadLettersControllerTests() => _sut = new AdminDeadLettersController(_queryService, _commandService, NullLogger<AdminDeadLettersController>.Instance) {
        ControllerContext = new ControllerContext {
            HttpContext = new DefaultHttpContext {
                User = CreatePrincipal("Operator", "tenant-a"),
            },
        }
    };

    [Fact]
    public async Task ListDeadLetters_DelegatesToQueryService() {
        var expected = new PagedResult<DeadLetterEntry>([], 0, null);
        _ = _queryService.ListDeadLettersAsync(Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        IActionResult result = await _sut.ListDeadLetters("tenant-a");

        OkObjectResult okResult = result.ShouldBeOfType<OkObjectResult>();
        okResult.Value.ShouldBe(expected);
    }

    [Fact]
    public async Task RetryDeadLetters_DelegatesToCommandService() {
        var expected = new AdminOperationResult(true, "op-1", "Retried", null);
        _ = _commandService.RetryDeadLettersAsync("tenant-a", Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        IActionResult result = await _sut.RetryDeadLetters("tenant-a", new DeadLetterActionRequest(["msg-1"]));

        OkObjectResult okResult = result.ShouldBeOfType<OkObjectResult>();
        okResult.Value.ShouldBe(expected);
    }

    [Fact]
    public async Task SkipDeadLetters_DelegatesToCommandService() {
        var expected = new AdminOperationResult(true, "op-2", "Skipped", null);
        _ = _commandService.SkipDeadLettersAsync("tenant-a", Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        IActionResult result = await _sut.SkipDeadLetters("tenant-a", new DeadLetterActionRequest(["msg-1"]));

        OkObjectResult okResult = result.ShouldBeOfType<OkObjectResult>();
        okResult.Value.ShouldBe(expected);
    }

    [Fact]
    public async Task ArchiveDeadLetters_DelegatesToCommandService() {
        var expected = new AdminOperationResult(true, "op-3", "Archived", null);
        _ = _commandService.ArchiveDeadLettersAsync("tenant-a", Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        IActionResult result = await _sut.ArchiveDeadLetters("tenant-a", new DeadLetterActionRequest(["msg-1"]));

        OkObjectResult okResult = result.ShouldBeOfType<OkObjectResult>();
        okResult.Value.ShouldBe(expected);
    }

    private static ClaimsPrincipal CreatePrincipal(string adminRole, params string[] tenants) {
        var claims = new List<Claim> { new(AdminClaimTypes.AdminRole, adminRole) };
        foreach (string tenant in tenants) {
            claims.Add(new Claim(AdminClaimTypes.Tenant, tenant));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }
}
