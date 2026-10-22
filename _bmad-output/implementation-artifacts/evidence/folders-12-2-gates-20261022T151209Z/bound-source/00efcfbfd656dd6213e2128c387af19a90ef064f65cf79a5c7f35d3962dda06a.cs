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
    private const string OpaqueVerdictValue = "opaque-verdict-value";

    [Fact]
    public async Task Exactly_one_accepting_policy_passes_the_reserved_extension_to_admission() {
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>())
            .Returns(new SubmitCommandResult("correlation-1"));
        CommandsController controller = CreateController(
            mediator,
            new TestLogger<CommandsController>(),
            [new PredicatePolicy(
                static (_, _, key) => key == ReservedKey,
                static (_, _, _, value) => value == OpaqueVerdictValue)]);

        IActionResult result = await controller.Submit(Request(), CancellationToken.None);

        result.ShouldBeOfType<AcceptedResult>();
        await mediator.Received(1).Send(
            Arg.Is<SubmitCommand>(command => command.Extensions != null
                && command.Extensions[ReservedKey] == OpaqueVerdictValue),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public async Task Zero_or_overlapping_claiming_policies_reject_before_admission(int acceptingPolicies) {
        IMediator mediator = Substitute.For<IMediator>();
        var logger = new TestLogger<CommandsController>();
        ITrustedCommandExtensionPolicy[] policies = Enumerable.Range(0, acceptingPolicies)
            .Select(_ => (ITrustedCommandExtensionPolicy)new PredicatePolicy(
                static (_, _, _) => true,
                static (_, _, _, _) => true))
            .ToArray();
        CommandsController controller = CreateController(mediator, logger, policies);

        IActionResult result = await controller.Submit(Request(), CancellationToken.None);

        ObjectResult problem = result.ShouldBeOfType<ObjectResult>();
        problem.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        problem.ContentTypes.ShouldContain("application/problem+json");
        await mediator.DidNotReceiveWithAnyArgs().Send(default!, default);
        logger.Messages.ShouldContain(message => message.Contains(ReservedKey, StringComparison.Ordinal));
        logger.Messages.ShouldAllBe(message => !message.Contains(OpaqueVerdictValue, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Only_the_single_claiming_policy_receives_the_reserved_extension_value() {
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>())
            .Returns(new SubmitCommandResult("correlation-1"));
        bool unclaimedPolicyReceivedValue = false;
        bool claimingPolicyReceivedExtensionDictionary = false;
        CommandsController controller = CreateController(
            mediator,
            new TestLogger<CommandsController>(),
            [
                new PredicatePolicy(
                    static (_, _, _) => false,
                    (_, _, _, _) => {
                        unclaimedPolicyReceivedValue = true;
                        return false;
                    }),
                new PredicatePolicy(
                    static (_, _, key) => key == ReservedKey,
                    (_, command, _, value) => {
                        claimingPolicyReceivedExtensionDictionary = command.Extensions is not null;
                        return value == OpaqueVerdictValue;
                    }),
            ]);

        IActionResult result = await controller.Submit(Request(), CancellationToken.None);

        result.ShouldBeOfType<AcceptedResult>();
        unclaimedPolicyReceivedValue.ShouldBeFalse();
        claimingPolicyReceivedExtensionDictionary.ShouldBeFalse();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Policy_exceptions_fail_closed_without_logging_the_extension_value(bool throwWhileClaiming) {
        IMediator mediator = Substitute.For<IMediator>();
        var logger = new TestLogger<CommandsController>();
        var policy = new PredicatePolicy(
            (_, _, _) => throwWhileClaiming
                ? throw new InvalidOperationException(OpaqueVerdictValue)
                : true,
            (_, _, _, _) => !throwWhileClaiming
                ? throw new InvalidOperationException(OpaqueVerdictValue)
                : true);
        CommandsController controller = CreateController(mediator, logger, [policy]);

        IActionResult result = await controller.Submit(Request(), CancellationToken.None);

        result.ShouldBeOfType<ObjectResult>().StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        await mediator.DidNotReceiveWithAnyArgs().Send(default!, default);
        logger.Messages.ShouldAllBe(message => !message.Contains(OpaqueVerdictValue, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Caller_supplied_global_admin_extension_is_ignored_without_policy_evaluation() {
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>())
            .Returns(new SubmitCommandResult("correlation-1"));
        var policy = new PredicatePolicy(
            static (_, _, _) => throw new InvalidOperationException("Policy must not inspect actor:globalAdmin."),
            static (_, _, _, _) => throw new InvalidOperationException("Policy must not receive actor:globalAdmin."));
        CommandsController controller = CreateController(mediator, new TestLogger<CommandsController>(), [policy]);
        SubmitCommandRequest request = Request() with {
            Extensions = new Dictionary<string, string> { ["actor:globalAdmin"] = "true" },
        };

        IActionResult result = await controller.Submit(request, CancellationToken.None);

        result.ShouldBeOfType<AcceptedResult>();
        await mediator.Received(1).Send(
            Arg.Is<SubmitCommand>(command => command.Extensions == null),
            Arg.Any<CancellationToken>());
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
        Extensions: new Dictionary<string, string> { [ReservedKey] = OpaqueVerdictValue },
        IdempotencyKey: "message-1");

    private sealed class PredicatePolicy(
        Func<string, string, string, bool> claims,
        Func<ClaimsPrincipal, SubmitCommandRequest, string, string, bool> accepts)
        : ITrustedCommandExtensionPolicy {
        public bool Claims(string domain, string commandType, string key)
            => claims(domain, commandType, key);

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
