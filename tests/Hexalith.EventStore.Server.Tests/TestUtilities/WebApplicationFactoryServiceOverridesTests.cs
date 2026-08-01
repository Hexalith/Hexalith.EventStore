extern alias eventstore;

using System.Runtime.CompilerServices;

using Hexalith.EventStore.Extensions;
using Hexalith.EventStore.Indexes;
using Hexalith.EventStore.Server.Projections;
using Hexalith.EventStore.Server.Tests.Integration;
using Hexalith.EventStore.Server.Tests.OpenApi;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Shouldly;

using EventStoreProgram = eventstore::Program;

namespace Hexalith.EventStore.Server.Tests.TestUtilities;

/// <summary>
/// Verifies the structural guarantees of the no-sidecar test-host override.
/// </summary>
public class WebApplicationFactoryServiceOverridesTests {
    [Fact]
    public void RemoveAdminOperationalIndexHostedService_RealRegistrations_RemovesOnlyHostedAlias() {
        var services = new ServiceCollection();
        _ = services.AddEventStore();
        ServiceDescriptor concreteService = services.Single(
            static descriptor => descriptor.ServiceType == typeof(AdminOperationalIndexHostedService));
        ServiceDescriptor refresherAlias = services.Single(
            static descriptor => descriptor.ServiceType == typeof(INamedProjectionCatalogRefresher));
        ServiceDescriptor hostedAlias = GetAdminOperationalIndexHostedAlias(services, concreteService, refresherAlias);
        ServiceDescriptor unrelatedHostedService = ServiceDescriptor.Singleton<IHostedService, SentinelHostedService>();
        ((IServiceCollection)services).Add(unrelatedHostedService);
        int registrationCount = services.Count;
        int hostedServiceCount = services.Count(static descriptor => descriptor.ServiceType == typeof(IHostedService));

        WebApplicationFactoryServiceOverrides.RemoveAdminOperationalIndexHostedService(services);

        services.Count.ShouldBe(registrationCount - 1);
        services.Count(static descriptor => descriptor.ServiceType == typeof(IHostedService))
            .ShouldBe(hostedServiceCount - 1);
        services.ShouldContain(concreteService);
        services.ShouldContain(refresherAlias);
        services.ShouldNotContain(hostedAlias);
        services.ShouldContain(unrelatedHostedService);
    }

    [Fact]
    public void RemoveAdminOperationalIndexHostedService_MissingHostedAlias_ThrowsWithoutMutation() {
        var services = new ServiceCollection();
        _ = services.AddEventStore();
        ServiceDescriptor concreteService = services.Single(
            static descriptor => descriptor.ServiceType == typeof(AdminOperationalIndexHostedService));
        ServiceDescriptor refresherAlias = services.Single(
            static descriptor => descriptor.ServiceType == typeof(INamedProjectionCatalogRefresher));
        ServiceDescriptor hostedAlias = GetAdminOperationalIndexHostedAlias(services, concreteService, refresherAlias);
        _ = services.Remove(hostedAlias);
        int registrationCount = services.Count;

        InvalidOperationException exception = Should.Throw<InvalidOperationException>(
            () => WebApplicationFactoryServiceOverrides.RemoveAdminOperationalIndexHostedService(services));

        exception.Message.ShouldContain("Unexpected AdminOperationalIndexHostedService registration layout");
        services.Count.ShouldBe(registrationCount);
        services.ShouldContain(concreteService);
        services.ShouldContain(refresherAlias);
    }

    [Fact]
    public void RemoveAdminOperationalIndexHostedService_AdjacentHostedDecoy_ThrowsWithoutMutation() {
        var services = new ServiceCollection();
        _ = services.AddEventStore();
        ServiceDescriptor concreteService = services.Single(
            static descriptor => descriptor.ServiceType == typeof(AdminOperationalIndexHostedService));
        ServiceDescriptor refresherAlias = services.Single(
            static descriptor => descriptor.ServiceType == typeof(INamedProjectionCatalogRefresher));
        ServiceDescriptor hostedAlias = GetAdminOperationalIndexHostedAlias(services, concreteService, refresherAlias);
        ServiceDescriptor decoy = ServiceDescriptor.Singleton<IHostedService>(
            static _ => new SentinelHostedService());
        services.Insert(services.IndexOf(hostedAlias), decoy);
        int registrationCount = services.Count;

        InvalidOperationException exception = Should.Throw<InvalidOperationException>(
            () => WebApplicationFactoryServiceOverrides.RemoveAdminOperationalIndexHostedService(services));

        exception.Message.ShouldContain("the hosted alias does not resolve AdminOperationalIndexHostedService");
        services.Count.ShouldBe(registrationCount);
        services.ShouldContain(decoy);
        services.ShouldContain(hostedAlias);
    }

    [Fact]
    public void RemoveAdminOperationalIndexHostedService_MissingRefresherAlias_ThrowsWithoutMutation() {
        var services = new ServiceCollection();
        _ = services.AddEventStore();
        ServiceDescriptor refresherAlias = services.Single(
            static descriptor => descriptor.ServiceType == typeof(INamedProjectionCatalogRefresher));
        _ = services.Remove(refresherAlias);
        int registrationCount = services.Count;

        InvalidOperationException exception = Should.Throw<InvalidOperationException>(
            () => WebApplicationFactoryServiceOverrides.RemoveAdminOperationalIndexHostedService(services));

        exception.Message.ShouldContain("the INamedProjectionCatalogRefresher alias");
        services.Count.ShouldBe(registrationCount);
    }

    [Fact]
    public void RemoveAdminOperationalIndexHostedService_DuplicateConcreteRegistration_ThrowsWithoutMutation() {
        var services = new ServiceCollection();
        _ = services.AddEventStore();
        ServiceDescriptor duplicate = ServiceDescriptor.Singleton<
            AdminOperationalIndexHostedService,
            AdminOperationalIndexHostedService>();
        ((IServiceCollection)services).Add(duplicate);
        int registrationCount = services.Count;

        InvalidOperationException exception = Should.Throw<InvalidOperationException>(
            () => WebApplicationFactoryServiceOverrides.RemoveAdminOperationalIndexHostedService(services));

        exception.Message.ShouldContain("expected exactly one concrete AdminOperationalIndexHostedService singleton");
        services.Count.ShouldBe(registrationCount);
        services.ShouldContain(duplicate);
    }

    [Fact]
    public void NoSidecarFactories_FinalHostedServices_ExcludeAdminOperationalIndex() {
        using var actorFactory = new ActorBasedAuthWebApplicationFactory();
        using WebApplicationFactory<EventStoreProgram> queryFactory = actorFactory.WithWebHostBuilder(static _ => { });
        using var signalRFactory = new SignalRHubWebApplicationFactory();
        using var signalRDisabledFactory = new SignalRDisabledWebApplicationFactory();
        using var eTagFactory = new ETagActorIntegrationTests.ETagTestFactory();
        using var openApiFactory = new OpenApiWebApplicationFactory();
        WebApplicationFactory<EventStoreProgram>[] factories = [
            actorFactory,
            queryFactory,
            signalRFactory,
            signalRDisabledFactory,
            eTagFactory,
            openApiFactory,
        ];

        foreach (WebApplicationFactory<EventStoreProgram> factory in factories) {
            using HttpClient client = factory.CreateClient();
            IHostedService[] hostedServices = [.. factory.Services.GetServices<IHostedService>()];

            hostedServices.Any(static service => service is AdminOperationalIndexHostedService)
                .ShouldBeFalse($"{factory.GetType().Name} must not start the operational index without Dapr");
            factory.Services.GetRequiredService<INamedProjectionCatalogRefresher>()
                .ShouldBeSameAs(factory.Services.GetRequiredService<AdminOperationalIndexHostedService>());
        }
    }

    private static ServiceDescriptor GetAdminOperationalIndexHostedAlias(
        IServiceCollection services,
        ServiceDescriptor concreteService,
        ServiceDescriptor refresherAlias) {
        int concreteIndex = services.IndexOf(concreteService);
        services[concreteIndex + 1].ShouldBeSameAs(refresherAlias);
        ServiceDescriptor hostedAlias = services[concreteIndex + 2];
        hostedAlias.ServiceType.ShouldBe(typeof(IHostedService));
        hostedAlias.ImplementationFactory.ShouldNotBeNull();

        var probe = (AdminOperationalIndexHostedService)RuntimeHelpers.GetUninitializedObject(
            typeof(AdminOperationalIndexHostedService));
        var probeProvider = new AdminOperationalIndexProbeServiceProvider(probe);
        refresherAlias.ImplementationFactory!(probeProvider).ShouldBeSameAs(probe);
        hostedAlias.ImplementationFactory!(probeProvider).ShouldBeSameAs(probe);
        return hostedAlias;
    }

    private sealed class AdminOperationalIndexProbeServiceProvider(AdminOperationalIndexHostedService service)
        : IServiceProvider {
        public object? GetService(Type serviceType) =>
            serviceType == typeof(AdminOperationalIndexHostedService) ? service : null;
    }

    private sealed class SentinelHostedService : IHostedService {
        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
