using System.Security.Claims;
using System.Text.Json;
using Hexalith.EventStore.Authorization;
using Hexalith.EventStore.Configuration;
using Hexalith.EventStore.Contracts.Authorization;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Controllers;
using Hexalith.EventStore.Server.Identity;
using Hexalith.EventStore.Server.Queries;
using Hexalith.EventStore.Validation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Controllers;

public sealed class IdentityGatewayDenialTests
{
    [Fact]
    public async Task IdentityCommandWithoutProvider_DeniesBeforeMediator()
    {
        IMediator mediator = Substitute.For<IMediator>();
        var controller = new CommandsController(mediator, new(Options.Create(new ExtensionMetadataOptions())), NullLogger<CommandsController>.Instance)
            { ControllerContext = Context() };
        var request = new SubmitCommandRequest("01HX0000000000000000000001", "tenant-a", "party", "party-1", "ProvisionAgentParty", JsonSerializer.SerializeToElement(new { logicalId = "logical" }));
        (await controller.Submit(request, TestContext.Current.CancellationToken)).ShouldBeOfType<StatusCodeResult>().StatusCode.ShouldBe(403);
        await mediator.DidNotReceiveWithAnyArgs().Send(default!, default);
    }

    [Theory]
    [InlineData(false, "party-1")]
    [InlineData(true, "party-other")]
    public async Task IdentityQueryMissingProviderOrForeignEntity_DeniesBeforeAdmissionAndMediator(bool install, string entity)
    {
        IMediator mediator = Substitute.For<IMediator>();
        ITenantValidator tenants = Substitute.For<ITenantValidator>();
        tenants.ValidateAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<string?>()).Returns(TenantValidationResult.Allowed);
        IRbacValidator rbac = Substitute.For<IRbacValidator>();
        rbac.ValidateAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<string?>()).Returns(RbacValidationResult.Allowed);
        IIdentityGatewayAdmission admission = Substitute.For<IIdentityGatewayAdmission>();
        var controller = new QueriesController(mediator, Substitute.For<IETagService>(), tenants, rbac, NullLogger<QueriesController>.Instance, install ? admission : null)
            { ControllerContext = Context() };
        var request = new SubmitQueryRequest("tenant-a", "party", "party-1", "ResolvePartyIdentity", Payload: JsonSerializer.SerializeToElement(new { tenantId = "tenant-a", partyId = "party-1" }), EntityId: entity);
        (await controller.Submit(request, null, TestContext.Current.CancellationToken)).ShouldBeOfType<StatusCodeResult>().StatusCode.ShouldBe(403);
        await mediator.DidNotReceiveWithAnyArgs().Send(default!, default);
        await admission.DidNotReceiveWithAnyArgs().AdmitAsync(default!, default!, default!, default);
    }

    private static ControllerContext Context() => new() { HttpContext = new DefaultHttpContext
        { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "test-user")], "test")) } };
}
