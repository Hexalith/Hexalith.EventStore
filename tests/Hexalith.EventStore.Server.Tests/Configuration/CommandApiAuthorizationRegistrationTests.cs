using Dapr.Actors.Client;

using Hexalith.EventStore.CommandApi.Authorization;
using Hexalith.EventStore.CommandApi.Extensions;
using Hexalith.EventStore.CommandApi.Pipeline;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Pipeline.Commands;
using Hexalith.EventStore.Testing.Fakes;

using MediatR;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Configuration;

public class CommandApiAuthorizationRegistrationTests {
    private static ServiceProvider BuildProvider(Dictionary<string, string?>? configValues = null) {
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
    public void AddCommandApi_MediatRPipelineOrdering_IsCorrect() {
        // Arrange
        using ServiceProvider provider = BuildProvider();
        using IServiceScope scope = provider.CreateScope();

        // Act
        var behaviors = scope.ServiceProvider.GetServices<IPipelineBehavior<SubmitCommand, SubmitCommandResult>>().ToList();

        // Assert
        // Behaviors should be ordered as added in AddOpenBehavior: Logging -> Validation -> Authorization
        int loggingIndex = behaviors.FindIndex(b => b.GetType().Name.Contains("LoggingBehavior"));
        int validationIndex = behaviors.FindIndex(b => b.GetType().Name.Contains("ValidationBehavior"));
        int authorizationIndex = behaviors.FindIndex(b => b.GetType().Name.Contains("AuthorizationBehavior"));

        loggingIndex.ShouldBeGreaterThan(-1);
        validationIndex.ShouldBeGreaterThan(loggingIndex);
        authorizationIndex.ShouldBeGreaterThan(validationIndex);
    }
}
