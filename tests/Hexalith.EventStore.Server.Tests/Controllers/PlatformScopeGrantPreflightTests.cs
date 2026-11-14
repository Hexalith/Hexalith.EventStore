using System.Security.Claims;
using System.Text.Json;

using Hexalith.EventStore.Authorization;
using Hexalith.EventStore.Configuration;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Controllers;
using Hexalith.EventStore.Server.Queries;
using Hexalith.EventStore.ServiceDefaults.Routing;
using Hexalith.EventStore.Validation;

using MediatR;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Controllers;

/// <summary>Verifies platform request grants are checked before catalog acquisition.</summary>
public sealed class PlatformScopeGrantPreflightTests
{
    /// <summary>Rejects malformed, reserved and normalized-duplicate grants before downstream work.</summary>
    /// <param name="query">Whether to exercise the query rather than command boundary.</param>
    /// <param name="extraGrant">The invalid or duplicate grant added to the principal.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Theory]
    [InlineData(false, " acme")]
    [InlineData(false, "SYSTEM")]
    [InlineData(false, "acme")]
    [InlineData(true, " acme")]
    [InlineData(true, "SYSTEM")]
    [InlineData(true, "acme")]
    public async Task InvalidPlatformGrantsNeverReachCatalogOrDispatch(bool query, string extraGrant)
    {
        var authority = Substitute.For<IRouteCatalogAuthority>();
        authority.AcquireAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException<RouteCatalogDispatchLease>(new RouteCatalogUnavailableException("Unavailable")));
        var mediator = Substitute.For<IMediator>();
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([
                new Claim("sub", "administrator"), new Claim("eventstore:tenant", "ACME"),
                new Claim("eventstore:tenant", extraGrant), new Claim("eventstore:permission", "tenants:read"),
                new Claim("eventstore:permission", "tenants:create"),
            ], "test")),
        };

        IActionResult result = await SubmitAsync(query, http, authority, mediator);

        result.ShouldBeOfType<ForbidResult>();
        authority.ReceivedCalls().ShouldBeEmpty();
        mediator.ReceivedCalls().ShouldBeEmpty();
    }

    /// <summary>Retains independent catalog authorization without requiring a tenant grant for platform scope.</summary>
    /// <param name="query">Whether to exercise the query rather than command boundary.</param>
    /// <param name="tenantGrants">Whether to include valid distinct tenant grants.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ExplicitPlatformScopeStillRequiresCatalogWithEmptyOrDistinctTenantGrants(bool query, bool tenantGrants)
    {
        var authority = Substitute.For<IRouteCatalogAuthority>();
        authority.AcquireAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException<RouteCatalogDispatchLease>(new RouteCatalogUnavailableException("Unavailable")));
        var mediator = Substitute.For<IMediator>();
        var claims = new List<Claim> { new("sub", "administrator"), new("eventstore:permission", "tenants:read"), new("eventstore:permission", "tenants:create") };
        if (tenantGrants)
            claims.AddRange([new("eventstore:tenant", "ACME"), new("eventstore:tenant", "other")]);
        var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) };

        await Should.ThrowAsync<RouteCatalogUnavailableException>(() => SubmitAsync(query, http, authority, mediator));

        await authority.Received(1).AcquireAsync(Arg.Any<CancellationToken>());
        mediator.ReceivedCalls().ShouldBeEmpty();
    }

    private static Task<IActionResult> SubmitAsync(bool query, HttpContext http, IRouteCatalogAuthority authority, IMediator mediator)
    {
        if (query)
        {
            var controller = new QueriesController(authority, mediator, Substitute.For<IETagService>(),
                Substitute.For<ITenantValidator>(), Substitute.For<IRbacValidator>(), NullLogger<QueriesController>.Instance)
            {
                ControllerContext = new ControllerContext { HttpContext = http },
            };
            return controller.Submit(new SubmitQueryRequest("platform:tenants", "tenants", "index", "ListTenants", null, null),
                null, TestContext.Current.CancellationToken);
        }

        var commands = new CommandsController(mediator, new ExtensionMetadataSanitizer(Options.Create(new ExtensionMetadataOptions())),
            NullLogger<CommandsController>.Instance, catalogAuthority: authority)
        {
            ControllerContext = new ControllerContext { HttpContext = http },
        };
        return commands.Submit(new SubmitCommandRequest("01JAAAAAAAAAAAAAAAAAAAAAAA", "platform:tenants", "tenants", "acme",
            "CreateTenant", JsonSerializer.SerializeToElement(new { tenantId = "acme", name = "Acme" })), TestContext.Current.CancellationToken);
    }
}
