using System.Security.Claims;
using System.Text.Json;

using Dapr.Actors.Client;

using Hexalith.EventStore.Authorization;
using Hexalith.EventStore.Authentication;
using Hexalith.EventStore.Configuration;
using Hexalith.EventStore.Controllers;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Pipeline.Commands;
using Hexalith.EventStore.Server.Tests.Fakes;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.EventStore.Testing.Fakes;
using Hexalith.EventStore.Validation;

using MediatR;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Controllers;

public sealed class WorkloadGatewayControllerTests
{
    [Fact]
    public void Workload_routes_require_distinct_signed_operation_policies()
    {
        typeof(CommandsController).GetMethod(nameof(CommandsController.SubmitWorkload))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), false)
            .Cast<AuthorizeAttribute>().ShouldHaveSingleItem().Policy
            .ShouldBe(DaprInternalAuthenticationOptions.GatewayCommandSubmitPolicy);
        typeof(CommandStatusController).GetMethod(nameof(CommandStatusController.GetWorkloadStatus))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), false)
            .Cast<AuthorizeAttribute>().ShouldHaveSingleItem().Policy
            .ShouldBe(DaprInternalAuthenticationOptions.GatewayCommandStatusPolicy);
        typeof(StreamsController).GetMethod(nameof(StreamsController.ReadWorkloadStreamAsync))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), false)
            .Cast<AuthorizeAttribute>().ShouldHaveSingleItem().Policy
            .ShouldBe(DaprInternalAuthenticationOptions.GatewayStreamReadPolicy);
    }

    [Fact]
    public async Task Submit_stamps_verified_workload_and_bound_actor()
    {
        IMediator mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>())
            .Returns(new SubmitCommandResult("correlation-1"));
        CommandsController controller = CommandController(mediator, "tenant-a", "operator-1");

        IActionResult result = await controller.SubmitWorkload(CommandRequest(), CancellationToken.None);

        result.ShouldBeOfType<AcceptedResult>();
        controller.Response.Headers.Location.ToString().ShouldBe(
            "https://localhost/api/v1/commands/status/workload/tenant-a/message-123");
        await mediator.Received(1).Send(Arg.Is<SubmitCommand>(command =>
            command.UserId == "operator-1"
            && command.Extensions != null
            && command.Extensions[EventStoreGatewayVerifiedOrigin.ExtensionKey] == "timesheets"
            && command.Extensions[EventStoreGatewayVerifiedOrigin.ActorExtensionKey] == "operator-1"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Submit_rejects_cross_tenant_or_caller_supplied_origin_before_dispatch()
    {
        IMediator mediator = Substitute.For<IMediator>();
        CommandsController controller = CommandController(mediator, "tenant-b");
        (await controller.SubmitWorkload(CommandRequest(), CancellationToken.None))
            .ShouldBeOfType<ForbidResult>();

        controller = CommandController(mediator, "tenant-a");
        (await controller.SubmitWorkload(CommandRequest() with { Domain = "projects" }, CancellationToken.None))
            .ShouldBeOfType<ForbidResult>();
        SubmitCommandRequest forged = CommandRequest() with
        {
            Extensions = new Dictionary<string, string>
            {
                [EventStoreGatewayVerifiedOrigin.ExtensionKey] = "timesheets"
            }
        };
        (await controller.SubmitWorkload(forged, CancellationToken.None))
            .ShouldBeOfType<BadRequestObjectResult>();
        await mediator.DidNotReceiveWithAnyArgs().Send(default!, default);
    }

    [Fact]
    public async Task Status_uses_bound_tenant_and_message_primary_record_only()
    {
        const string messageId = "message-123";
        var store = new InMemoryCommandStatusStore();
        await store.WriteStatusAsync("tenant-a", messageId,
            new CommandStatusRecord(CommandStatus.Completed, DateTimeOffset.UtcNow,
                "entry-1", 2, null, null, null, messageId, "correlation-1")
                { Domain = "timesheets", CommittedEventSequence = 7 }, CancellationToken.None);
        var controller = new CommandStatusController(store, NullLogger<CommandStatusController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = Context("tenant-a") }
        };

        var response = (await controller.GetWorkloadStatus("tenant-a", messageId, CancellationToken.None))
            .ShouldBeOfType<OkObjectResult>().Value
            .ShouldBeOfType<Hexalith.EventStore.Models.CommandStatusResponse>();
        response.TenantId.ShouldBe("tenant-a");
        response.Domain.ShouldBe("timesheets");
        response.AggregateId.ShouldBe("entry-1");
        response.EventCount.ShouldBe(2);
        response.CommittedEventSequence.ShouldBe(7);
        (await controller.GetWorkloadStatus("tenant-b", messageId, CancellationToken.None))
            .ShouldBeOfType<ForbidResult>();
        (await controller.GetWorkloadStatus("tenant-a", "correlation-1", CancellationToken.None))
            .ShouldBeOfType<NotFoundResult>();
        await store.WriteStatusAsync("tenant-a", "foreign-message",
            new CommandStatusRecord(CommandStatus.Completed, DateTimeOffset.UtcNow,
                "project-1", 1, null, null, null, "foreign-message", "foreign-correlation")
                { Domain = "projects" }, CancellationToken.None);
        (await controller.GetWorkloadStatus("tenant-a", "foreign-message", CancellationToken.None))
            .ShouldBeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Stream_read_requires_bound_tenant_and_skips_human_tenant_and_rbac_checks()
    {
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        actor.GetStreamMetadataAsync().Returns(new AggregateStreamMetadata(false, 0));
        IActorProxyFactory proxy = Substitute.For<IActorProxyFactory>();
        proxy.CreateActorProxy<IAggregateActor>(Arg.Any<Dapr.Actors.ActorId>(), "AggregateActor", Arg.Any<ActorProxyOptions?>())
            .Returns(actor);
        var tenantValidator = new FakeTenantValidator { ConfiguredResult = TenantValidationResult.Denied("denied") };
        var rbacValidator = new FakeRbacValidator { ConfiguredResult = RbacValidationResult.Denied("denied") };
        var controller = new StreamsController(proxy, tenantValidator, rbacValidator,
            NullLogger<StreamsController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = Context("tenant-a") }
        };

        IActionResult result = await controller.ReadWorkloadStreamAsync(
            new StreamReadRequest("tenant-a", "timesheets", "entry-1"));
        result.ShouldBeOfType<ObjectResult>().StatusCode.ShouldBe(StatusCodes.Status404NotFound);
        tenantValidator.ReceivedRequests.ShouldBeEmpty();
        rbacValidator.ReceivedRequests.ShouldBeEmpty();

        (await controller.ReadWorkloadStreamAsync(
            new StreamReadRequest("tenant-b", "timesheets", "entry-1")))
            .ShouldBeOfType<ForbidResult>();
        (await controller.ReadWorkloadStreamAsync(
            new StreamReadRequest("tenant-a", "projects", "entry-1")))
            .ShouldBeOfType<ForbidResult>();
    }

    private static CommandsController CommandController(IMediator mediator, string tenant, string? actor = null)
    {
        var controller = new CommandsController(mediator,
            new ExtensionMetadataSanitizer(Options.Create(new ExtensionMetadataOptions())),
            NullLogger<CommandsController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = Context(tenant, actor) }
        };
        controller.HttpContext.Request.Scheme = "https";
        controller.HttpContext.Request.Host = new HostString("localhost");
        return controller;
    }

    private static DefaultHttpContext Context(string tenant, string? actor = null)
    {
        var claims = new List<Claim>
        {
            new(EventStoreWorkloadAuthenticationDefaults.WorkloadClaimType, "timesheets"),
            new(EventStoreWorkloadAuthenticationDefaults.TenantBindingClaimType, tenant),
            new(EventStoreWorkloadAuthenticationDefaults.DomainBindingClaimType, "timesheets")
        };
        if (actor is not null)
        {
            claims.Add(new Claim(EventStoreWorkloadAuthenticationDefaults.ActorBindingClaimType, actor));
        }

        return new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "workload"))
        };
    }

    private static SubmitCommandRequest CommandRequest() => new(
        "message-123", "tenant-a", "timesheets", "entry-1", "CommitMagicLinkUse",
        JsonSerializer.SerializeToElement(new { }));
}
