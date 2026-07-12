using Dapr.Actors.Runtime;
using Dapr.Client;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Projections;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Configuration;

/// <summary>
/// Task 8 DI verification for the coordinated projection-erase graph. Asserts that
/// <see cref="EventStoreServerServiceCollectionExtensions.AddEventStoreServer"/> registers a
/// <see cref="ServiceDescriptor"/> for every erase-path contract (the released canonical seams and the
/// Server-internal opt-in erase capabilities, including the read-model conditional eraser bound on the
/// shared read-model store singleton), that the coordinator resolves end-to-end, and that the
/// <see cref="ProjectionLifecycleActor"/> is registered with the actor runtime.
/// </summary>
public class ProjectionEraseRegistrationTests {
    [Theory]
    [InlineData(typeof(IProjectionEraseCoordinator))]
    [InlineData(typeof(IProjectionReadModelAddressFactory))]
    [InlineData(typeof(IProjectionSlotRegistry))]
    [InlineData(typeof(IReadModelConditionalEraser))]
    [InlineData(typeof(IProjectionRebuildCheckpointEraser))]
    [InlineData(typeof(IProjectionDeliveryCheckpointStore))]
    [InlineData(typeof(IProjectionLifecycleGateway))]
    public void AddEventStoreServer_RegistersProjectionEraseServiceDescriptor(Type serviceType) {
        ArgumentNullException.ThrowIfNull(serviceType);

        IServiceCollection services = BuildServices(registerDaprClient: false);

        services.ShouldContain(
            descriptor => descriptor.ServiceType == serviceType,
            $"AddEventStoreServer must register a ServiceDescriptor for {serviceType.Name}.");
    }

    [Fact]
    public void AddEventStoreServer_ResolvesProjectionEraseCoordinatorEndToEnd() {
        using ServiceProvider provider = BuildServices(registerDaprClient: true).BuildServiceProvider();

        IProjectionEraseCoordinator coordinator = provider.GetRequiredService<IProjectionEraseCoordinator>();

        _ = coordinator.ShouldBeOfType<ProjectionEraseCoordinator>();
    }

    [Fact]
    public void AddEventStoreServer_ResolvesReadModelConditionalEraserSharingTheReadModelStoreSingleton() {
        using ServiceProvider provider = BuildServices(registerDaprClient: true).BuildServiceProvider();

        IReadModelConditionalEraser eraser = provider.GetRequiredService<IReadModelConditionalEraser>();
        IReadModelStore store = provider.GetRequiredService<IReadModelStore>();

        ReferenceEquals(eraser, store).ShouldBeTrue(
            "the conditional eraser must be bound on the same singleton as the released read-model store.");
    }

    [Fact]
    public void AddEventStoreServer_RegistersProjectionLifecycleActor() {
        using ServiceProvider provider = BuildServices(registerDaprClient: false).BuildServiceProvider();

        ActorRuntime actorRuntime = provider.GetRequiredService<ActorRuntime>();

        actorRuntime.RegisteredActors.ShouldContain(registration =>
            registration.Type.ImplementationType == typeof(ProjectionLifecycleActor)
            && registration.Type.ActorTypeName == ProjectionLifecycleActor.ActorTypeName);
    }

    private static IServiceCollection BuildServices(bool registerDaprClient) {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection([])
            .Build();
        var services = new ServiceCollection();
        _ = services.AddLogging();
        if (registerDaprClient) {
            _ = services.AddSingleton(Substitute.For<DaprClient>());
        }

        _ = services.AddEventStoreServer(configuration);
        return services;
    }
}
