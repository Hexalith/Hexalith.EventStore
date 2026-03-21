using Dapr.Client;

using Hexalith.EventStore.Admin.Abstractions.Services;
using Hexalith.EventStore.Admin.Server.Configuration;
using Hexalith.EventStore.Admin.Server.Services;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using NSubstitute;

namespace Hexalith.EventStore.Admin.Server.Tests.Configuration;

public class ServiceCollectionExtensionsTests
{
    private static (IServiceCollection Services, IConfiguration Configuration) CreateServicesWithConfig()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AdminServer:StateStoreName"] = "teststore",
                ["AdminServer:CommandApiAppId"] = "test-commandapi",
                ["AdminServer:TenantServiceAppId"] = "test-tenants",
            })
            .Build();

        var services = new ServiceCollection();

        // Add logging and DaprClient mock (required by service constructors)
        services.AddLogging();
        services.AddScoped(_ => Substitute.For<DaprClient>());

        return (services, config);
    }

    [Fact]
    public void AddAdminServer_RegistersAllServiceInterfaces()
    {
        (IServiceCollection services, IConfiguration config) = CreateServicesWithConfig();

        services.AddAdminServer(config);

        using ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        IServiceProvider sp = scope.ServiceProvider;

        sp.GetService<IStreamQueryService>().ShouldNotBeNull();
        sp.GetService<IProjectionQueryService>().ShouldNotBeNull();
        sp.GetService<IProjectionCommandService>().ShouldNotBeNull();
        sp.GetService<ITypeCatalogService>().ShouldNotBeNull();
        sp.GetService<IHealthQueryService>().ShouldNotBeNull();
        sp.GetService<IStorageQueryService>().ShouldNotBeNull();
        sp.GetService<IStorageCommandService>().ShouldNotBeNull();
        sp.GetService<IDeadLetterQueryService>().ShouldNotBeNull();
        sp.GetService<IDeadLetterCommandService>().ShouldNotBeNull();
        sp.GetService<ITenantQueryService>().ShouldNotBeNull();
    }

    [Fact]
    public void AddAdminServer_RegistersAuthContext()
    {
        (IServiceCollection services, IConfiguration config) = CreateServicesWithConfig();

        services.AddAdminServer(config);

        using ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        IAdminAuthContext authContext = scope.ServiceProvider.GetRequiredService<IAdminAuthContext>();
        authContext.ShouldBeOfType<NullAdminAuthContext>();
    }

    [Fact]
    public void AddAdminServer_BindsOptionsFromConfiguration()
    {
        (IServiceCollection services, IConfiguration config) = CreateServicesWithConfig();

        services.AddAdminServer(config);

        using ServiceProvider provider = services.BuildServiceProvider();
        AdminServerOptions options = provider
            .GetRequiredService<Microsoft.Extensions.Options.IOptions<AdminServerOptions>>()
            .Value;

        options.StateStoreName.ShouldBe("teststore");
        options.CommandApiAppId.ShouldBe("test-commandapi");
        options.TenantServiceAppId.ShouldBe("test-tenants");
    }

    [Fact]
    public void AddAdminServer_NullConfiguration_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();
        Should.Throw<ArgumentNullException>(() => services.AddAdminServer(null!));
    }

    [Fact]
    public void AddAdminServer_UsesDefaultOptions_WhenNotConfigured()
    {
        var config = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddAdminServer(config);

        using ServiceProvider provider = services.BuildServiceProvider();
        AdminServerOptions options = provider
            .GetRequiredService<Microsoft.Extensions.Options.IOptions<AdminServerOptions>>()
            .Value;

        options.StateStoreName.ShouldBe("statestore");
        options.CommandApiAppId.ShouldBe("commandapi");
        options.TenantServiceAppId.ShouldBe("tenants");
        options.MaxTimelineEvents.ShouldBe(1000);
        options.ServiceInvocationTimeoutSeconds.ShouldBe(30);
    }
}
