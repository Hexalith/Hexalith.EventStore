using Dapr.Actors.Client;

using Hexalith.EventStore.CommandApi.Authorization;
using Hexalith.EventStore.CommandApi.Extensions;
using Hexalith.EventStore.CommandApi.Middleware;
using Hexalith.EventStore.CommandApi.Pipeline;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Pipeline.Commands;
using Hexalith.EventStore.Testing.Fakes;

using MediatR;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using NSubstitute;

using System.Security.Claims;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Configuration;

public class CommandApiAuthorizationRegistrationTests {
    private static ServiceProvider BuildProvider(
        Dictionary<string, string?>? configValues = null,
        Action<IServiceCollection>? configureServices = null) {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues ?? [])
            .Build();

        var services = new ServiceCollection();
        _ = services.AddSingleton(configuration);
        _ = services.AddLogging();
        _ = services.AddSingleton<ICommandStatusStore>(new InMemoryCommandStatusStore());
        _ = services.AddSingleton<ICommandArchiveStore>(new InMemoryCommandArchiveStore());

        // Actor-based validators require IActorProxyFactory in the container
        _ = services.AddSingleton(Substitute.For<IActorProxyFactory>());

        _ = services.AddCommandApi();
        configureServices?.Invoke(services);

        return services.BuildServiceProvider();
    }

    [Fact]
    public void AddCommandApi_DefaultAuthorizationOptions_ResolveClaimsTenantValidator() {
        using ServiceProvider provider = BuildProvider();
        using IServiceScope scope = provider.CreateScope();

        ITenantValidator validator = scope.ServiceProvider.GetRequiredService<ITenantValidator>();

        _ = validator.ShouldBeOfType<ClaimsTenantValidator>();
    }

    [Fact]
    public void AddCommandApi_DefaultAuthorizationOptions_ResolveClaimsRbacValidator() {
        using ServiceProvider provider = BuildProvider();
        using IServiceScope scope = provider.CreateScope();

        IRbacValidator validator = scope.ServiceProvider.GetRequiredService<IRbacValidator>();

        _ = validator.ShouldBeOfType<ClaimsRbacValidator>();
    }

    [Fact]
    public void AddCommandApi_ConfiguredTenantActor_ResolvesActorTenantValidator() {
        using ServiceProvider provider = BuildProvider(
            new Dictionary<string, string?> {
                ["EventStore:Authorization:TenantValidatorActorName"] = "TenantValidatorActor",
            });
        using IServiceScope scope = provider.CreateScope();

        ITenantValidator validator = scope.ServiceProvider.GetRequiredService<ITenantValidator>();

        _ = validator.ShouldBeOfType<ActorTenantValidator>();
    }

    [Fact]
    public void AddCommandApi_ConfiguredRbacActor_ResolvesActorRbacValidator() {
        using ServiceProvider provider = BuildProvider(
            new Dictionary<string, string?> {
                ["EventStore:Authorization:RbacValidatorActorName"] = "RbacValidatorActor",
            });
        using IServiceScope scope = provider.CreateScope();

        IRbacValidator validator = scope.ServiceProvider.GetRequiredService<IRbacValidator>();

        _ = validator.ShouldBeOfType<ActorRbacValidator>();
    }

    [Fact]
    public async Task AddCommandApi_ConfiguredTenantActor_PassesStartupValidationAsync() {
        using ServiceProvider provider = BuildProvider(
            new Dictionary<string, string?> {
                ["EventStore:Authorization:TenantValidatorActorName"] = "TenantValidatorActor",
            });

        IHostedService startupValidator = provider
            .GetServices<IHostedService>()
            .Single(service => service.GetType().Name == "CommandApiAuthorizationStartupValidator");

        // Should NOT throw — actor-based implementation is now registered
        await startupValidator.StartAsync(CancellationToken.None);
    }

    [Fact]
    public async Task AddCommandApi_ConfiguredRbacActor_PassesStartupValidationAsync() {
        using ServiceProvider provider = BuildProvider(
            new Dictionary<string, string?> {
                ["EventStore:Authorization:RbacValidatorActorName"] = "RbacValidatorActor",
            });

        IHostedService startupValidator = provider
            .GetServices<IHostedService>()
            .Single(service => service.GetType().Name == "CommandApiAuthorizationStartupValidator");

        // Should NOT throw — actor-based implementation is now registered
        await startupValidator.StartAsync(CancellationToken.None);
    }

    [Fact]
    public void AddCommandApi_ClaimsTenantWithActorRbac_ResolvesMixedConfig() {
        using ServiceProvider provider = BuildProvider(
            new Dictionary<string, string?> {
                // TenantValidatorActorName is null (default) → claims-based
                ["EventStore:Authorization:RbacValidatorActorName"] = "RbacValidatorActor",
            });
        using IServiceScope scope = provider.CreateScope();

        ITenantValidator tenantValidator = scope.ServiceProvider.GetRequiredService<ITenantValidator>();
        IRbacValidator rbacValidator = scope.ServiceProvider.GetRequiredService<IRbacValidator>();

        _ = tenantValidator.ShouldBeOfType<ClaimsTenantValidator>();
        _ = rbacValidator.ShouldBeOfType<ActorRbacValidator>();
    }

    [Fact]
    public void AddCommandApi_DefaultConfig_ResolvesAuthorizationBehaviorWithValidators() {
        // Task 6.1-6.2: Verify AuthorizationBehavior resolves from real DI with correct validators
        using ServiceProvider provider = BuildProvider();
        using IServiceScope scope = provider.CreateScope();

        IEnumerable<IPipelineBehavior<SubmitCommand, SubmitCommandResult>> behaviors =
            scope.ServiceProvider.GetServices<IPipelineBehavior<SubmitCommand, SubmitCommandResult>>();

        IPipelineBehavior<SubmitCommand, SubmitCommandResult>? authBehavior = behaviors
            .FirstOrDefault(b => b.GetType().GetGenericTypeDefinition() == typeof(AuthorizationBehavior<,>));

        _ = authBehavior.ShouldNotBeNull("AuthorizationBehavior should be resolved from DI container");

        // Also verify the validators are the claims-based defaults
        ITenantValidator tenantValidator = scope.ServiceProvider.GetRequiredService<ITenantValidator>();
        IRbacValidator rbacValidator = scope.ServiceProvider.GetRequiredService<IRbacValidator>();
        _ = tenantValidator.ShouldBeOfType<ClaimsTenantValidator>();
        _ = rbacValidator.ShouldBeOfType<ClaimsRbacValidator>();
    }

    [Fact]
    public async Task AuthorizationBehavior_ValidatorReturnsNull_ThrowsInvalidOperationException() {
        // Task 6.3: Defensive test — null result should throw InvalidOperationException (500), not NullReferenceException
        using ServiceProvider provider = BuildProvider();
        using IServiceScope scope = provider.CreateScope();

        ITenantValidator nullReturningValidator = Substitute.For<ITenantValidator>();
        _ = nullReturningValidator.ValidateAsync(Arg.Any<System.Security.Claims.ClaimsPrincipal>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((TenantValidationResult)null!);

        IRbacValidator rbacValidator = new ClaimsRbacValidator();

        var httpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext {
            User = new System.Security.Claims.ClaimsPrincipal(
                new System.Security.Claims.ClaimsIdentity(
                    [new System.Security.Claims.Claim("sub", "user")], "test")),
        };
        httpContext.Items["CorrelationId"] = "test-correlation-id";

        Microsoft.AspNetCore.Http.IHttpContextAccessor accessor = Substitute.For<Microsoft.AspNetCore.Http.IHttpContextAccessor>();
        _ = accessor.HttpContext.Returns(httpContext);

        ILogger<AuthorizationBehavior<SubmitCommand, SubmitCommandResult>> logger = Substitute.For<Microsoft.Extensions.Logging.ILogger<AuthorizationBehavior<SubmitCommand, SubmitCommandResult>>>();
        var behavior = new AuthorizationBehavior<SubmitCommand, SubmitCommandResult>(accessor, nullReturningValidator, rbacValidator, logger);

        var command = new SubmitCommand("msg-1", "test-tenant", "test-domain", "agg-1", "TestCmd", [0x01], "corr-1", "user");
        var next = new RequestHandlerDelegate<SubmitCommandResult>((_) => Task.FromResult(new SubmitCommandResult("corr-1")));

        // Should throw InvalidOperationException (server bug → 500), NOT NullReferenceException
        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(
            () => behavior.Handle(command, next, CancellationToken.None));
        ex.Message.ShouldContain("server bug");
    }

    [Fact]
    public void AddCommandApi_ActorTenantWithClaimsRbac_ResolvesMixedConfig() {
        using ServiceProvider provider = BuildProvider(
            new Dictionary<string, string?> {
                ["EventStore:Authorization:TenantValidatorActorName"] = "TenantValidatorActor",
                // RbacValidatorActorName is null (default) → claims-based
            });
        using IServiceScope scope = provider.CreateScope();

        ITenantValidator tenantValidator = scope.ServiceProvider.GetRequiredService<ITenantValidator>();
        IRbacValidator rbacValidator = scope.ServiceProvider.GetRequiredService<IRbacValidator>();

        _ = tenantValidator.ShouldBeOfType<ActorTenantValidator>();
        _ = rbacValidator.ShouldBeOfType<ClaimsRbacValidator>();
    }

    [Fact]
    public async Task AddCommandApi_MediatRPipelineOrdering_ExecutesInCorrectOrderAsync() {
        // Arrange
        List<string> executionOrder = [];
        using ServiceProvider provider = BuildProvider(
            configureServices: services => {
                _ = services.AddLogging(builder => {
                    builder.ClearProviders();
                    builder.SetMinimumLevel(LogLevel.Debug);
                    builder.AddProvider(new PipelineOrderLoggerProvider(executionOrder));
                });

                services.RemoveAll<IRequestHandler<SubmitCommand, SubmitCommandResult>>();
                _ = services.AddScoped<IRequestHandler<SubmitCommand, SubmitCommandResult>>(_ => new PipelineOrderSubmitCommandHandler(executionOrder));
            });
        using IServiceScope scope = provider.CreateScope();

        Type[] behaviorOrder = scope.ServiceProvider
            .GetServices<IPipelineBehavior<SubmitCommand, SubmitCommandResult>>()
            .Select(b => b.GetType().GetGenericTypeDefinition())
            .ToArray();

        IHttpContextAccessor httpContextAccessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        httpContextAccessor.HttpContext = new DefaultHttpContext {
            User = new ClaimsPrincipal(
                new ClaimsIdentity(
                    [
                        new Claim("sub", "test-user"),
                        new Claim("eventstore:tenant", "test-tenant"),
                    ],
                    "test")),
        };
        httpContextAccessor.HttpContext.Items[CorrelationIdMiddleware.HttpContextKey] = "test-correlation-id";

        IMediator mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var command = new SubmitCommand(
            "msg-1",
            "test-tenant",
            "test-domain",
            "agg-1",
            "CreateOrder",
            [0x01],
            "test-correlation-id",
            "test-user");

        // Act
        SubmitCommandResult result = await mediator.Send(command, CancellationToken.None);

        // Assert
        result.CorrelationId.ShouldBe("test-correlation-id");
        behaviorOrder.ShouldBe([
            typeof(LoggingBehavior<,>),
            typeof(ValidationBehavior<,>),
            typeof(AuthorizationBehavior<,>),
        ]);
        executionOrder.ShouldBe([
            "LoggingBehavior.Entry",
            "ValidationBehavior.Passed",
            "AuthorizationBehavior.Passed",
            "Handler.Execute",
            "LoggingBehavior.Exit",
        ]);
    }

    private sealed class PipelineOrderSubmitCommandHandler(List<string> executionOrder)
        : IRequestHandler<SubmitCommand, SubmitCommandResult> {
        public Task<SubmitCommandResult> Handle(SubmitCommand request, CancellationToken cancellationToken) {
            executionOrder.Add("Handler.Execute");
            return Task.FromResult(new SubmitCommandResult(request.CorrelationId));
        }
    }

    private sealed class PipelineOrderLoggerProvider(List<string> executionOrder) : ILoggerProvider {
        public ILogger CreateLogger(string categoryName) => new PipelineOrderLogger(categoryName, executionOrder);

        public void Dispose() {
        }
    }

    private sealed class PipelineOrderLogger(string categoryName, List<string> executionOrder) : ILogger {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) {
            string? marker = categoryName switch {
                var value when value.Contains(nameof(LoggingBehavior<object, object>), StringComparison.Ordinal)
                    && eventId.Id == 1000 => "LoggingBehavior.Entry",
                var value when value.Contains(nameof(LoggingBehavior<object, object>), StringComparison.Ordinal)
                    && eventId.Id == 1001 => "LoggingBehavior.Exit",
                var value when value.Contains(nameof(ValidationBehavior<object, object>), StringComparison.Ordinal)
                    && eventId.Id == 1010 => "ValidationBehavior.Passed",
                var value when value.Contains(nameof(AuthorizationBehavior<object, object>), StringComparison.Ordinal)
                    && eventId.Id == 1020 => "AuthorizationBehavior.Passed",
                _ => null,
            };

            if (marker is not null) {
                executionOrder.Add(marker);
            }
        }
    }
}
