using System.Security.Claims;
using System.Text.Json;

using Hexalith.EventStore.Authorization;
using Hexalith.EventStore.Configuration;
using Hexalith.EventStore.Controllers;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Middleware;
using Hexalith.EventStore.Server.Pipeline.Commands;
using Hexalith.EventStore.Validation;

using MediatR;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Controllers;

/// <summary>
/// Fail-closed gateway tests for colon-namespaced trusted command extensions.
/// </summary>
public sealed class CommandsControllerTrustedExtensionTests {
    private const string ReservedKey = "provider:selectionValidation";
    private const string SecretValue = "SECRET-VERDICT-VALUE";

    [Fact]
    public async Task Exactly_one_accepting_policy_passes_the_reserved_extension_to_admission() {
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>())
            .Returns(new SubmitCommandResult("correlation-1"));
        CommandsController controller = CreateController(
            mediator,
            new TestLogger<CommandsController>(),
            [new PredicatePolicy(static (_, _, key, value) => key == ReservedKey && value == SecretValue)]);

        IActionResult result = await controller.Submit(Request(), CancellationToken.None);

        result.ShouldBeOfType<AcceptedResult>();
        await mediator.Received(1).Send(
            Arg.Is<SubmitCommand>(command => command.Extensions != null
                && command.Extensions[ReservedKey] == SecretValue),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public async Task Zero_or_overlapping_accepting_policies_reject_before_admission(int acceptingPolicies) {
        IMediator mediator = Substitute.For<IMediator>();
        var logger = new TestLogger<CommandsController>();
        ITrustedCommandExtensionPolicy[] policies = Enumerable.Range(0, acceptingPolicies)
            .Select(_ => (ITrustedCommandExtensionPolicy)new PredicatePolicy(static (_, _, _, _) => true))
            .ToArray();
        CommandsController controller = CreateController(mediator, logger, policies);

        IActionResult result = await controller.Submit(Request(), CancellationToken.None);

        ObjectResult problem = result.ShouldBeOfType<ObjectResult>();
        problem.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        problem.ContentTypes.ShouldContain("application/problem+json");
        await mediator.DidNotReceiveWithAnyArgs().Send(default!, default);
        logger.Messages.ShouldContain(message => message.Contains(ReservedKey, StringComparison.Ordinal));
        logger.Messages.ShouldAllBe(message => !message.Contains(SecretValue, StringComparison.Ordinal));
    }

    private static CommandsController CreateController(
        IMediator mediator,
        ILogger<CommandsController> logger,
        IEnumerable<ITrustedCommandExtensionPolicy> policies) {
        ExtensionMetadataSanitizer sanitizer = new(Options.Create(new ExtensionMetadataOptions()));
        var controller = new CommandsController(mediator, sanitizer, logger, policies);
        var httpContext = new DefaultHttpContext {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "system:agents")], "test")),
        };
        httpContext.Items[CorrelationIdMiddleware.HttpContextKey] = "correlation-1";
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("localhost");
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }

    private static SubmitCommandRequest Request() => new(
        MessageId: "message-1",
        Tenant: "tenant-a",
        Domain: "agent",
        AggregateId: "agent-a",
        CommandType: "ActivateAgent",
        Payload: JsonSerializer.SerializeToElement(new { }),
        CorrelationId: "correlation-1",
        Extensions: new Dictionary<string, string> { [ReservedKey] = SecretValue },
        IdempotencyKey: "message-1");

    private sealed class PredicatePolicy(
        Func<ClaimsPrincipal, SubmitCommandRequest, string, string, bool> accepts)
        : ITrustedCommandExtensionPolicy {
        public bool Accepts(ClaimsPrincipal principal, SubmitCommandRequest command, string key, string value)
            => accepts(principal, command, key, value);
    }

    private sealed class TestLogger<T> : ILogger<T> {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Messages.Add(formatter(state, exception));
    }
}
