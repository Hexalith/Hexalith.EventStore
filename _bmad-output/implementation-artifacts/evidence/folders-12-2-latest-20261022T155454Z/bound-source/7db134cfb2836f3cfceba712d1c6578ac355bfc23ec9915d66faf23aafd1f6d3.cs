using System.Security.Claims;
using System.Text.Json;

using Hexalith.EventStore.Configuration;
using Hexalith.EventStore.Controllers;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Effects;
using Hexalith.EventStore.Middleware;
using Hexalith.EventStore.Server.Pipeline.Commands;
using Hexalith.EventStore.Validation;

using MediatR;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Controllers;

/// <summary>Ordinary command ingress cannot enter the trusted-effect identifier namespace.</summary>
public sealed class CommandsControllerTrustedEffectNamespaceTests
{
    /// <summary>A reserved message identifier or idempotency key is refused before gateway admission.</summary>
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task ReservedEffectIdentifierIsRejectedBeforeAdmission(bool reservedMessageId, bool reservedIdempotencyKey)
    {
        IMediator mediator = Substitute.For<IMediator>();
        CommandsController controller = CreateController(mediator);
        var identity = new EffectIdentity(
            "tenant-a", "works", "source-1", 1,
            EffectKindCatalog.DateResume, "works", "target-1", 0);
        string effectMessageId = EffectIdentityCodec.ComputeMessageId(identity);
        var request = new SubmitCommandRequest(
            MessageId: reservedMessageId ? effectMessageId : "message-1",
            Tenant: "tenant-a",
            Domain: "works",
            AggregateId: "target-1",
            CommandType: "ResumeWorkItem",
            Payload: JsonSerializer.SerializeToElement(new { }),
            CorrelationId: "correlation-1",
            IdempotencyKey: reservedIdempotencyKey ? effectMessageId : "message-1");

        IActionResult result = await controller.Submit(request, CancellationToken.None);

        _ = result.ShouldBeOfType<BadRequestObjectResult>();
        await mediator.DidNotReceiveWithAnyArgs().Send(default!, default);
    }

    /// <summary>An ordinary identifier still reaches gateway admission.</summary>
    [Fact]
    public async Task OrdinaryIdentifierReachesAdmission()
    {
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>())
            .Returns(new SubmitCommandResult("correlation-1"));
        CommandsController controller = CreateController(mediator);
        var request = new SubmitCommandRequest(
            MessageId: "message-1",
            Tenant: "tenant-a",
            Domain: "works",
            AggregateId: "target-1",
            CommandType: "ResumeWorkItem",
            Payload: JsonSerializer.SerializeToElement(new { }),
            CorrelationId: "correlation-1",
            IdempotencyKey: "message-1");

        IActionResult result = await controller.Submit(request, CancellationToken.None);

        _ = result.ShouldBeOfType<AcceptedResult>();
        await mediator.Received(1).Send(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>());
    }

    private static CommandsController CreateController(IMediator mediator)
    {
        ExtensionMetadataSanitizer sanitizer = new(Options.Create(new ExtensionMetadataOptions()));
        var controller = new CommandsController(mediator, sanitizer, NullLogger<CommandsController>.Instance, []);
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "system:agents")], "test")),
        };
        httpContext.Items[CorrelationIdMiddleware.HttpContextKey] = "correlation-1";
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("localhost");
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }
}
