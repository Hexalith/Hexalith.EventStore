using System.Security.Claims;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Models.DeadLetters;
using Hexalith.EventStore.Admin.Abstractions.Services;
using Hexalith.EventStore.Admin.Server.Authorization;
using Hexalith.EventStore.Admin.Server.Controllers;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

namespace Hexalith.EventStore.Admin.Server.Tests.Controllers;

/// <summary>
/// Verifies the unfiltered dead-letter listing cannot widen a non-admin caller to the global backlog.
/// </summary>
/// <remarks>
/// A request that names a tenant is already checked against the caller's claims by
/// <see cref="AdminTenantAuthorizationFilter"/>. The uncovered case is the request that names none. The Operator
/// role is granted by <c>eventstore:permission=command:replay</c> alone and carries no tenant claim requirement,
/// so resolving an absent scope to "global" hands that caller every tenant's retained dead letters. This was
/// latent while the backing index was never populated; the operations workload returns real items.
/// </remarks>
public sealed class AdminDeadLettersControllerTenantScopeTests {
    private readonly IDeadLetterCommandService _commandService = Substitute.For<IDeadLetterCommandService>();
    private readonly IDeadLetterQueryService _queryService = Substitute.For<IDeadLetterQueryService>();

    /// <summary>Verifies a non-admin caller with no tenant claim is refused instead of scoped globally.</summary>
    [Fact]
    public async Task ListDeadLettersDeniesUnscopedNonAdminCaller() {
        AdminDeadLettersController controller = CreateController(CreatePrincipal("Operator", tenant: null));

        IActionResult result = await controller.ListDeadLetters(null);

        result.ShouldBeOfType<ObjectResult>().StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
        _ = await _queryService.DidNotReceive().ListDeadLettersAsync(
            Arg.Any<string?>(),
            Arg.Any<int>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    /// <summary>Verifies a non-admin caller's own tenant claim still scopes the unfiltered listing.</summary>
    [Fact]
    public async Task ListDeadLettersScopesNonAdminCallerToItsTenantClaim() {
        AdminDeadLettersController controller = CreateController(CreatePrincipal("Operator", "tenant-a"));
        _ = _queryService
            .ListDeadLettersAsync("tenant-a", Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<DeadLetterEntry>([], 0, null));

        IActionResult result = await controller.ListDeadLetters(null);

        _ = result.ShouldBeOfType<OkObjectResult>();
        _ = await _queryService.Received(1).ListDeadLettersAsync(
            "tenant-a",
            Arg.Any<int>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    /// <summary>Verifies the Admin role keeps the global listing.</summary>
    [Fact]
    public async Task ListDeadLettersKeepsGlobalScopeForAdminRole() {
        AdminDeadLettersController controller = CreateController(CreatePrincipal("Admin", tenant: null));
        _ = _queryService
            .ListDeadLettersAsync(Arg.Is((string?)null), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<DeadLetterEntry>([], 0, null));

        IActionResult result = await controller.ListDeadLetters(null);

        _ = result.ShouldBeOfType<OkObjectResult>();
        _ = await _queryService.Received(1).ListDeadLettersAsync(
            Arg.Is((string?)null),
            Arg.Any<int>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Verifies an authorization refusal from the operations workload reaches the operator as Forbidden.
    /// </summary>
    /// <remarks>
    /// Reporting it as the transient 503 the outage path uses would tell the operator to retry a refusal that
    /// retrying cannot clear.
    /// </remarks>
    [Fact]
    public async Task ReadSurfacesMapOperationsRefusalToForbidden() {
        AdminDeadLettersController controller = CreateController(CreatePrincipal("Admin", tenant: null));
        _ = _queryService
            .ListDeadLettersAsync(Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns<PagedResult<DeadLetterEntry>>(_ => throw new UnauthorizedAccessException("denied"));
        _ = _queryService.GetDeadLetterCountAsync(Arg.Any<CancellationToken>())
            .Returns<int>(_ => throw new UnauthorizedAccessException("denied"));

        IActionResult list = await controller.ListDeadLetters(null);
        IActionResult count = await controller.GetDeadLetterCount();

        list.ShouldBeOfType<ObjectResult>().StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
        count.ShouldBeOfType<ObjectResult>().StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
    }

    private static ClaimsPrincipal CreatePrincipal(string adminRole, string? tenant) {
        List<Claim> claims = [new(AdminClaimTypes.AdminRole, adminRole)];
        if (tenant is not null) {
            claims.Add(new Claim(AdminClaimTypes.Tenant, tenant));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }

    private AdminDeadLettersController CreateController(ClaimsPrincipal principal)
        => new(_queryService, _commandService, NullLogger<AdminDeadLettersController>.Instance) {
            ControllerContext = new ControllerContext {
                HttpContext = new DefaultHttpContext { User = principal },
            },
        };
}
