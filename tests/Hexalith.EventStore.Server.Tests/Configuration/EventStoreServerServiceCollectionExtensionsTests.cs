using Dapr.Actors.Runtime;
using Dapr.Client;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Projections;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Configuration;

public class EventStoreServerServiceCollectionExtensionsTests {
    [Fact]
    public void AddEventStoreServerWithoutDaprClientDoesNotActivateCleanupService() {
        using ServiceProvider provider = BuildProvider(registerDaprClient: false);

        IHostedService[] hostedServices = [.. provider.GetServices<IHostedService>()];

        hostedServices.ShouldNotContain(service => service is ActiveRebuildIndexCleanupService);
    }

    [Fact]
    public void AddEventStoreServerWithDaprClientActivatesCleanupService() {
        using ServiceProvider provider = BuildProvider(registerDaprClient: true);

        IHostedService[] hostedServices = [.. provider.GetServices<IHostedService>()];

        hostedServices.ShouldContain(service => service is ActiveRebuildIndexCleanupService);
    }

    [Fact]
    public void AddEventStoreServerRegistersDaprGlobalPositionAllocator() {
        using ServiceProvider provider = BuildProvider(registerDaprClient: false);

        IGlobalPositionAllocator allocator = provider.GetRequiredService<IGlobalPositionAllocator>();

        _ = allocator.ShouldBeOfType<DaprGlobalPositionAllocator>();
    }

    [Fact]
    public void AddEventStoreServerRegistersGlobalPositionActor() {
        using ServiceProvider provider = BuildProvider(registerDaprClient: false);

        ActorRuntime actorRuntime = provider.GetRequiredService<ActorRuntime>();

        actorRuntime.RegisteredActors.ShouldContain(registration =>
            registration.Type.ImplementationType == typeof(GlobalPositionActor)
            && registration.Type.ActorTypeName == GlobalPositionActor.ActorTypeName);
    }

    [Fact]
    public void AddEventStoreServerRegistersDeliveryStateMachineAndReadyHealthGate() {
        using ServiceProvider provider = BuildProvider(registerDaprClient: true);

        provider.GetRequiredService<IProjectionDeliveryStateStore>()
            .ShouldBeOfType<DaprProjectionDeliveryStateStore>();
        provider.GetRequiredService<IProjectionDeliveryIdempotencyCoordinator>()
            .ShouldBeOfType<ProjectionDeliveryIdempotencyCoordinator>();
        provider.GetRequiredService<IProjectionDeliveryReconciler>()
            .ShouldBeOfType<ProjectionDeliveryReconciler>();
        provider.GetRequiredService<IProjectionDeliveryCutover>()
            .ShouldBeOfType<ProjectionDeliveryCutover>();
        provider.GetRequiredService<IOptions<ProjectionDeliveryIdempotencyOptions>>().Value
            .CompletedReceiptLimit.ShouldBe(256);
        HealthCheckRegistration registration = provider
            .GetRequiredService<IOptions<HealthCheckServiceOptions>>()
            .Value.Registrations
            .Single(value => value.Name == "projection-delivery-writer-protocol");
        registration.Tags.ShouldContain("ready");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(DomainServiceOptions.MaximumInvocationTimeoutSeconds + 1)]
    public void AddEventStoreServerRejectsInvocationTimeoutOutsideSupportedRange(int timeoutSeconds) {
        using ServiceProvider provider = BuildProvider(
            registerDaprClient: false,
            new Dictionary<string, string?> {
                ["EventStore:DomainServices:InvocationTimeoutSeconds"] = timeoutSeconds.ToString(
                    System.Globalization.CultureInfo.InvariantCulture),
            });

        _ = Should.Throw<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<DomainServiceOptions>>().Value);
    }

    [Fact]
    public void AddEventStoreServerAcceptsMaximumInvocationTimeout() {
        using ServiceProvider provider = BuildProvider(
            registerDaprClient: false,
            new Dictionary<string, string?> {
                ["EventStore:DomainServices:InvocationTimeoutSeconds"]
                    = DomainServiceOptions.MaximumInvocationTimeoutSeconds.ToString(
                        System.Globalization.CultureInfo.InvariantCulture),
            });

        provider.GetRequiredService<IOptions<DomainServiceOptions>>()
            .Value.InvocationTimeoutSeconds.ShouldBe(DomainServiceOptions.MaximumInvocationTimeoutSeconds);
    }

    private static ServiceProvider BuildProvider(
        bool registerDaprClient,
        IEnumerable<KeyValuePair<string, string?>>? configurationValues = null) {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationValues ?? [])
            .Build();
        var services = new ServiceCollection();
        _ = services.AddLogging();
        if (registerDaprClient) {
            _ = services.AddSingleton(Substitute.For<DaprClient>());
        }

        _ = services.AddEventStoreServer(configuration);
        return services.BuildServiceProvider();
    }
}
