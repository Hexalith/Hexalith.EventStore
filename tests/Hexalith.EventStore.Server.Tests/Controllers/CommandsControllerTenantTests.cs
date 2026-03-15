using System.Security.Claims;
using System.Text.Json;

using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.CommandApi.Authorization;
using Hexalith.EventStore.CommandApi.Configuration;
using Hexalith.EventStore.CommandApi.Controllers;
using Hexalith.EventStore.CommandApi.ErrorHandling;
using Hexalith.EventStore.CommandApi.Middleware;
using Hexalith.EventStore.CommandApi.Models;
using Hexalith.EventStore.CommandApi.Pipeline;
using Hexalith.EventStore.CommandApi.Validation;
using Hexalith.EventStore.Server.Actors.Authorization;
using Hexalith.EventStore.Server.Pipeline.Commands;

using MediatR;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Controllers;

/// <summary>
/// Delegation verification tests proving that CommandsController delegates tenant/auth
/// decisions to the MediatR pipeline (AuthorizationBehavior) after Story 17-3 refactoring.
/// Replaces the 7 characterization tests that tested inline tenant checks removed in 17-3.
/// </summary>
public class CommandsControllerTenantTests {
    private static SubmitCommandRequest CreateTestRequest(
        string tenant = "test-tenant",
        string domain = "test-domain",
        string commandType = "CreateOrder",
        string aggregateId = "agg-001") =>
        new(
            MessageId: Guid.NewGuid().ToString(),
            Tenant: tenant,
            Domain: domain,
            AggregateId: aggregateId,
            CommandType: commandType,
            Payload: JsonSerializer.SerializeToElement(new { Value = 1 }));

    private static (CommandsController Controller, IMediator Mediator) CreateControllerWithMediator(ClaimsPrincipal? principal = null) {
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>())
            .Returns(new SubmitCommandResult("test-correlation-id"));

        ExtensionMetadataSanitizer sanitizer = new(Options.Create(new ExtensionMetadataOptions()));
        ILogger<CommandsController> logger = Substitute.For<ILogger<CommandsController>>();

        var controller = new CommandsController(mediator, sanitizer, logger);

        var httpContext = new DefaultHttpContext();
        if (principal is not null) {
            httpContext.User = principal;
        }

        httpContext.Items[CorrelationIdMiddleware.HttpContextKey] = "test-correlation-id";

        controller.ControllerContext = new ControllerContext {
            HttpContext = httpContext,
        };

        return (controller, mediator);
    }

    private static ClaimsPrincipal CreateAuthenticatedPrincipal(params string[] tenants) {
        var claims = new List<Claim>();
        foreach (string t in tenants) {
            claims.Add(new Claim("eventstore:tenant", t));
        }

        claims.Add(new Claim("sub", "test-user"));
        claims.Add(new Claim(ClaimTypes.NameIdentifier, "test-user"));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    private static ClaimsPrincipal CreateAuthenticatedPrincipalWithoutTenants() =>
        new(new ClaimsIdentity(
        [
            new Claim("sub", "test-user"),
            new Claim(ClaimTypes.NameIdentifier, "test-user"),
        ],
        "test"));

    private static CommandsController CreateControllerWithRealAuthorizationPipeline(
        ClaimsPrincipal principal,
        ITenantValidator tenantValidator,
        IRbacValidator rbacValidator,
        out DefaultHttpContext httpContext) {
        var services = new ServiceCollection();
        var accessor = new HttpContextAccessor();

        _ = services.AddLogging();
        _ = services.AddSingleton<IHttpContextAccessor>(accessor);
        _ = services.AddSingleton(tenantValidator);
        _ = services.AddSingleton(rbacValidator);
        _ = services.AddSingleton<ITenantValidator>(tenantValidator);
        _ = services.AddSingleton<IRbacValidator>(rbacValidator);
        _ = services.AddTransient<IRequestHandler<SubmitCommand, SubmitCommandResult>, PipelineTestSubmitCommandHandler>();
        _ = services.AddMediatR(cfg => {
            _ = cfg.RegisterServicesFromAssemblyContaining<PipelineTestSubmitCommandHandler>();
            _ = cfg.AddOpenBehavior(typeof(AuthorizationBehavior<,>));
        });

        ServiceProvider provider = services.BuildServiceProvider();
        IMediator mediator = provider.GetRequiredService<IMediator>();
        ExtensionMetadataSanitizer sanitizer = new(Options.Create(new ExtensionMetadataOptions()));
        var controller = new CommandsController(mediator, sanitizer, NullLogger<CommandsController>.Instance);

        httpContext = new DefaultHttpContext {
            RequestServices = provider,
            User = principal,
        };

        httpContext.Items[CorrelationIdMiddleware.HttpContextKey] = "test-correlation-id";
        httpContext.Request.Path = "/api/v1/commands";
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("localhost");
        httpContext.Response.Body = new MemoryStream();

        accessor.HttpContext = httpContext;

        controller.ControllerContext = new ControllerContext {
            HttpContext = httpContext,
        };

        return controller;
    }

    private static ITenantValidator CreateUnavailableActorTenantValidator(int retryAfterSeconds) {
        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        ITenantValidatorActor actor = Substitute.For<ITenantValidatorActor>();
        _ = actor.ValidateTenantAccessAsync(Arg.Any<TenantValidationRequest>())
            .Throws(new HttpRequestException("Connection refused"));

        _ = factory.CreateActorProxy<ITenantValidatorActor>(Arg.Any<ActorId>(), Arg.Any<string>())
            .Returns(actor);

        return new ActorTenantValidator(
            factory,
            Options.Create(new EventStoreAuthorizationOptions {
                TenantValidatorActorName = "TestTenantValidatorActor",
                RetryAfterSeconds = retryAfterSeconds,
            }),
            NullLoggerFactory.Instance.CreateLogger<ActorTenantValidator>());
    }

    [Fact]
    public async Task Submit_ValidRequest_DelegatesToMediatR() {
        // Arrange — controller should delegate all auth to MediatR pipeline
        ClaimsPrincipal principal = CreateAuthenticatedPrincipal("test-tenant");
        (CommandsController controller, IMediator mediator) = CreateControllerWithMediator(principal);
        SubmitCommandRequest request = CreateTestRequest();

        // Act
        _ = await controller.Submit(request, CancellationToken.None);

        // Assert — MediatR.Send was called exactly once
        _ = await mediator.Received(1).Send(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Submit_ValidRequest_SendsCorrectCommandToMediatR() {
        // Arrange
        ClaimsPrincipal principal = CreateAuthenticatedPrincipal("test-tenant");
        (CommandsController controller, IMediator mediator) = CreateControllerWithMediator(principal);
        SubmitCommandRequest request = CreateTestRequest(tenant: "test-tenant", domain: "test-domain", commandType: "CreateOrder");

        // Act
        _ = await controller.Submit(request, CancellationToken.None);

        // Assert — verify command fields are passed through correctly
        _ = await mediator.Received(1).Send(
            Arg.Is<SubmitCommand>(cmd =>
                cmd.Tenant == "test-tenant" &&
                cmd.Domain == "test-domain" &&
                cmd.CommandType == "CreateOrder" &&
                cmd.AggregateId == "agg-001"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Submit_NoTenantClaims_StillDelegatesToMediatR() {
        // Arrange — no tenant claims; controller no longer checks this, behavior does
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "test-user")], "test"));
        (CommandsController controller, IMediator mediator) = CreateControllerWithMediator(principal);
        SubmitCommandRequest request = CreateTestRequest();

        // Act
        _ = await controller.Submit(request, CancellationToken.None);

        // Assert — controller sends to MediatR regardless of claims; behavior handles auth
        _ = await mediator.Received(1).Send(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Submit_MissingSubClaim_ReturnsUnauthorizedAndDoesNotCallMediator() {
        // Arrange
        var principal = new ClaimsPrincipal(new ClaimsIdentity([], "test"));
        (CommandsController controller, IMediator mediator) = CreateControllerWithMediator(principal);
        SubmitCommandRequest request = CreateTestRequest();

        // Act
        IActionResult result = await controller.Submit(request, CancellationToken.None);

        // Assert
        _ = result.ShouldBeOfType<UnauthorizedResult>();
        _ = await mediator.DidNotReceive().Send(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Submit_NoTenantClaims_WithRealPipeline_ThrowsCommandAuthorizationExceptionFromBehavior() {
        // Arrange — prove the controller delegates into the real MediatR pipeline where AuthorizationBehavior denies the request
        ClaimsPrincipal principal = CreateAuthenticatedPrincipalWithoutTenants();
        CommandsController controller = CreateControllerWithRealAuthorizationPipeline(
            principal,
            new ClaimsTenantValidator(),
            new ClaimsRbacValidator(),
            out _);
        SubmitCommandRequest request = CreateTestRequest();

        // Act & Assert
        CommandAuthorizationException ex = await Should.ThrowAsync<CommandAuthorizationException>(
            () => controller.Submit(request, CancellationToken.None));

        ex.Reason.ShouldContain("No tenant authorization claims");
    }

    [Fact]
    public async Task Submit_ActorTenantValidatorUnavailable_WithRealPipeline_HandlerReturns503WithRetryAfter() {
        // Arrange — prove the 503 path from controller -> MediatR pipeline -> AuthorizationBehavior -> actor-based validator -> exception handler
        ClaimsPrincipal principal = CreateAuthenticatedPrincipal("test-tenant");
        CommandsController controller = CreateControllerWithRealAuthorizationPipeline(
            principal,
            CreateUnavailableActorTenantValidator(42),
            new ClaimsRbacValidator(),
            out DefaultHttpContext httpContext);
        SubmitCommandRequest request = CreateTestRequest();
        var handler = new AuthorizationServiceUnavailableHandler(NullLogger<AuthorizationServiceUnavailableHandler>.Instance);

        // Act
        AuthorizationServiceUnavailableException ex = await Should.ThrowAsync<AuthorizationServiceUnavailableException>(
            () => controller.Submit(request, CancellationToken.None));
        bool handled = await handler.TryHandleAsync(httpContext, ex, CancellationToken.None);

        // Assert
        handled.ShouldBeTrue();
        httpContext.Response.StatusCode.ShouldBe(StatusCodes.Status503ServiceUnavailable);
        httpContext.Response.Headers.RetryAfter.ToString().ShouldBe("42");
    }

    [Fact]
    public async Task Submit_MediatRThrowsCommandAuthorizationException_Propagates() {
        // Arrange — MediatR pipeline (behavior) throws auth exception
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new CommandAuthorizationException("test-tenant", "test-domain", "CreateOrder", "Tenant access denied."));

        ExtensionMetadataSanitizer sanitizer = new(Options.Create(new ExtensionMetadataOptions()));
        ILogger<CommandsController> logger = Substitute.For<ILogger<CommandsController>>();

        var controller = new CommandsController(mediator, sanitizer, logger);
        var httpContext = new DefaultHttpContext {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "test-user")], "test")),
        };
        httpContext.Items[CorrelationIdMiddleware.HttpContextKey] = "test-correlation-id";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        SubmitCommandRequest request = CreateTestRequest();

        // Act & Assert — exception propagates (caught by AuthorizationExceptionHandler in real pipeline)
        _ = await Should.ThrowAsync<CommandAuthorizationException>(
            () => controller.Submit(request, CancellationToken.None));
    }

    [Fact]
    public async Task Submit_StoresRequestTenantIdInHttpContext() {
        // Arrange — verify RequestTenantId is set for rate limiter
        ClaimsPrincipal principal = CreateAuthenticatedPrincipal("test-tenant");
        (CommandsController controller, _) = CreateControllerWithMediator(principal);
        SubmitCommandRequest request = CreateTestRequest(tenant: "my-tenant");

        // Act
        _ = await controller.Submit(request, CancellationToken.None);

        // Assert
        controller.HttpContext.Items["RequestTenantId"].ShouldBe("my-tenant");
    }

    [Fact]
    public async Task Submit_AuthorizedTenantItem_NoLongerSetInHttpContext() {
        // Arrange — AuthorizedTenant was removed in 17-3 refactoring
        ClaimsPrincipal principal = CreateAuthenticatedPrincipal("test-tenant");
        (CommandsController controller, _) = CreateControllerWithMediator(principal);
        SubmitCommandRequest request = CreateTestRequest();

        // Act
        _ = await controller.Submit(request, CancellationToken.None);

        // Assert — AuthorizedTenant no longer set (moved to behavior layer)
        controller.HttpContext.Items.ShouldNotContainKey("AuthorizedTenant");
    }

    [Fact]
    public async Task Submit_SuccessfulRequest_Returns202Accepted() {
        // Arrange
        ClaimsPrincipal principal = CreateAuthenticatedPrincipal("test-tenant");
        (CommandsController controller, _) = CreateControllerWithMediator(principal);
        SubmitCommandRequest request = CreateTestRequest();

        // Act
        IActionResult result = await controller.Submit(request, CancellationToken.None);

        // Assert
        AcceptedResult accepted = result.ShouldBeOfType<AcceptedResult>();
        accepted.StatusCode.ShouldBe(StatusCodes.Status202Accepted);
    }

    private sealed class PipelineTestSubmitCommandHandler : IRequestHandler<SubmitCommand, SubmitCommandResult> {
        public Task<SubmitCommandResult> Handle(SubmitCommand request, CancellationToken cancellationToken) =>
            Task.FromResult(new SubmitCommandResult(request.CorrelationId));
    }
}
