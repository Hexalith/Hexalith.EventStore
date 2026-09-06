using Dapr.Client;

using Hexalith.EventStore.Admin.Abstractions.Services;
using Hexalith.EventStore.Admin.Server.Authorization;
using Hexalith.EventStore.Admin.Server.Configuration;
using Hexalith.EventStore.Admin.Server.Services;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using NSubstitute;

namespace Hexalith.EventStore.Admin.Server.Tests.Configuration;

public class ServiceCollectionExtensionsTests {
    private static (IServiceCollection Services, IConfiguration Configuration) CreateServicesWithConfig() {
        IConfigurationRoot config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["AdminServer:StateStoreName"] = "teststore",
                ["AdminServer:EventStoreAppId"] = "test-eventstore",
                ["AdminServer:TenantServiceAppId"] = "test-tenants",
            })
            .Build();

        var services = new ServiceCollection();

        // Add logging and DaprClient mock (required by service constructors)
        _ = services.AddLogging();
        _ = services.AddScoped(_ => Substitute.For<DaprClient>());

        return (services, config);
    }

    [Fact]
    public void AddAdminServer_RegistersAllServiceInterfaces() {
        (IServiceCollection services, IConfiguration config) = CreateServicesWithConfig();

        _ = services.AddAdminServer(config);

        using ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        IServiceProvider sp = scope.ServiceProvider;

        _ = sp.GetService<IStreamQueryService>().ShouldNotBeNull();
        _ = sp.GetService<IProjectionQueryService>().ShouldNotBeNull();
        _ = sp.GetService<IProjectionCommandService>().ShouldNotBeNull();
        _ = sp.GetService<ITypeCatalogService>().ShouldNotBeNull();
        _ = sp.GetService<IHealthQueryService>().ShouldNotBeNull();
        _ = sp.GetService<IStorageQueryService>().ShouldNotBeNull();
        _ = sp.GetService<IStorageCommandService>().ShouldNotBeNull();
        _ = sp.GetService<IDeadLetterQueryService>().ShouldNotBeNull();
        _ = sp.GetService<IDeadLetterCommandService>().ShouldNotBeNull();
        _ = sp.GetService<ITenantQueryService>().ShouldNotBeNull();
    }

    [Fact]
    public void AddAdminServer_RegistersAuthContext() {
        (IServiceCollection services, IConfiguration config) = CreateServicesWithConfig();

        _ = services.AddAdminServer(config);

        using ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        IAdminAuthContext authContext = scope.ServiceProvider.GetRequiredService<IAdminAuthContext>();
        _ = authContext.ShouldBeOfType<NullAdminAuthContext>();
    }

    [Fact]
    public void AddAdminServer_BindsOptionsFromConfiguration() {
        (IServiceCollection services, IConfiguration config) = CreateServicesWithConfig();

        _ = services.AddAdminServer(config);

        using ServiceProvider provider = services.BuildServiceProvider();
        AdminServerOptions options = provider
            .GetRequiredService<Microsoft.Extensions.Options.IOptions<AdminServerOptions>>()
            .Value;

        options.StateStoreName.ShouldBe("teststore");
        options.EventStoreAppId.ShouldBe("test-eventstore");
        options.TenantServiceAppId.ShouldBe("test-tenants");
    }

    [Fact]
    public void AddAdminServer_NullConfiguration_ThrowsArgumentNullException() {
        var services = new ServiceCollection();
        _ = Should.Throw<ArgumentNullException>(() => services.AddAdminServer(null!));
    }

    [Fact]
    public void AddAdminServer_UsesDefaultOptions_WhenNotConfigured() {
        IConfigurationRoot config = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        _ = services.AddLogging();

        _ = services.AddAdminServer(config);

        using ServiceProvider provider = services.BuildServiceProvider();
        AdminServerOptions options = provider
            .GetRequiredService<Microsoft.Extensions.Options.IOptions<AdminServerOptions>>()
            .Value;

        options.StateStoreName.ShouldBe("statestore");
        options.EventStoreAppId.ShouldBe("eventstore");
        options.TenantServiceAppId.ShouldBe("tenants");
        options.MaxTimelineEvents.ShouldBe(1000);
        options.ServiceInvocationTimeoutSeconds.ShouldBe(30);
    }

    [Fact]
    public async Task AddAdminApi_RegistersAuthorizationPolicies() {
        (IServiceCollection services, IConfiguration config) = CreateServicesWithConfig();
        _ = services.AddScoped(_ => Substitute.For<DaprClient>());

        _ = services.AddAdminApi(config);

        using ServiceProvider provider = services.BuildServiceProvider();
        IAuthorizationPolicyProvider policyProvider = provider.GetRequiredService<IAuthorizationPolicyProvider>();

        _ = (await policyProvider.GetPolicyAsync(AdminAuthorizationPolicies.ReadOnly)).ShouldNotBeNull();
        _ = (await policyProvider.GetPolicyAsync(AdminAuthorizationPolicies.Operator)).ShouldNotBeNull();
        _ = (await policyProvider.GetPolicyAsync(AdminAuthorizationPolicies.Admin)).ShouldNotBeNull();
    }

    [Theory]
    [InlineData(AdminAuthorizationPolicies.ReadOnly, "ReadOnly", true)]
    [InlineData(AdminAuthorizationPolicies.ReadOnly, "Operator", true)]
    [InlineData(AdminAuthorizationPolicies.ReadOnly, "Admin", true)]
    [InlineData(AdminAuthorizationPolicies.ReadOnly, "Unknown", false)]
    [InlineData(AdminAuthorizationPolicies.Operator, "ReadOnly", false)]
    [InlineData(AdminAuthorizationPolicies.Operator, "Operator", true)]
    [InlineData(AdminAuthorizationPolicies.Admin, "Operator", false)]
    [InlineData(AdminAuthorizationPolicies.Admin, "Admin", true)]
    public async Task AddAdminApi_PoliciesRequireAuthenticatedExactRole(
        string policyName,
        string role,
        bool expectedAuthorized) {
        (IServiceCollection services, IConfiguration config) = CreateServicesWithConfig();
        _ = services.AddAdminApi(config);

        using ServiceProvider provider = services.BuildServiceProvider();
        IAuthorizationService authorization = provider.GetRequiredService<IAuthorizationService>();
        var authenticated = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(
                [new System.Security.Claims.Claim(AdminClaimTypes.AdminRole, role)],
                "Test"));
        var unauthenticated = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(
                [new System.Security.Claims.Claim(AdminClaimTypes.AdminRole, role)]));

        AuthorizationResult authenticatedResult = await authorization
            .AuthorizeAsync(authenticated, null, policyName);
        AuthorizationResult unauthenticatedResult = await authorization
            .AuthorizeAsync(unauthenticated, null, policyName);

        authenticatedResult.Succeeded.ShouldBe(expectedAuthorized);
        unauthenticatedResult.Succeeded.ShouldBeFalse();
    }

    [Fact]
    public void AddAdminApi_ResolvesBoundedHandlerWithoutRemovingHostRegistration() {
        (IServiceCollection services, IConfiguration config) = CreateServicesWithConfig();
        IAuthorizationMiddlewareResultHandler hostHandler = Substitute.For<IAuthorizationMiddlewareResultHandler>();
        _ = services.AddSingleton(hostHandler);

        _ = services.AddAdminApi(config);

        using ServiceProvider provider = services.BuildServiceProvider();
        provider.GetRequiredService<IAuthorizationMiddlewareResultHandler>()
            .ShouldBeOfType<AdminAuthorizationMiddlewareResultHandler>();
        provider.GetServices<IAuthorizationMiddlewareResultHandler>().ShouldContain(hostHandler);
    }

    [Fact]
    public void AddAdminApi_RegistersTenantFilter() {
        (IServiceCollection services, IConfiguration config) = CreateServicesWithConfig();
        _ = services.AddScoped(_ => Substitute.For<DaprClient>());

        _ = services.AddAdminApi(config);

        using ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        _ = scope.ServiceProvider.GetService<AdminTenantAuthorizationFilter>().ShouldNotBeNull();
    }

    [Fact]
    public void AddAdminApi_RegistersClaimsTransformation() {
        (IServiceCollection services, IConfiguration config) = CreateServicesWithConfig();
        _ = services.AddScoped(_ => Substitute.For<DaprClient>());

        _ = services.AddAdminApi(config);

        using ServiceProvider provider = services.BuildServiceProvider();
        _ = provider.GetService<IClaimsTransformation>().ShouldNotBeNull();
        _ = provider.GetService<IClaimsTransformation>().ShouldBeOfType<AdminClaimsTransformation>();
    }
}
