using Dapr.Client;

using Hexalith.EventStore.Client.Registration;
using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Client.Subscriptions;
using Hexalith.EventStore.Contracts.Events;

using Microsoft.Extensions.DependencyInjection;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Registration;

public sealed class EventStoreDomainEventsServiceCollectionExtensionsTests {
    public sealed record RegistrationTestEvent(string AggregateId) : IEventPayload;

    private sealed class RegistrationTestHandler : IEventStoreDomainEventHandler<RegistrationTestEvent> {
        public Task HandleAsync(
            RegistrationTestEvent @event,
            EventStoreDomainEventContext context,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    [Fact]
    public void AddEventStoreDomainEvents_RegistersProcessorAndInMemoryMarkerStoreByDefault() {
        var services = new ServiceCollection();
        _ = services.AddLogging();

        _ = services.AddEventStoreDomainEvents(typeof(IEventPayload).Assembly);

        using ServiceProvider provider = services.BuildServiceProvider();
        _ = provider.GetRequiredService<EventStoreDomainEventProcessor>();
        IEventStoreDomainEventMarkerStore markerStore = provider.GetRequiredService<IEventStoreDomainEventMarkerStore>();
        _ = markerStore.ShouldBeOfType<InMemoryEventStoreDomainEventMarkerStore>();
    }

    [Fact]
    public void AddDaprEventStoreDomainEventMarkerStore_ReplacesDefaultMarkerStore() {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddSingleton(Substitute.For<DaprClient>());

        _ = services.AddEventStoreDomainEvents(typeof(RegistrationTestEvent).Assembly);
        _ = services.AddDaprEventStoreDomainEventMarkerStore();

        using ServiceProvider provider = services.BuildServiceProvider();
        IEventStoreDomainEventMarkerStore markerStore = provider.GetRequiredService<IEventStoreDomainEventMarkerStore>();
        _ = markerStore.ShouldBeOfType<DaprEventStoreDomainEventMarkerStore>();
    }

    [Fact]
    public void AddEventStoreDomainEvents_ValidatesUnobservedContractTypes() {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddEventStoreDomainEvents(typeof(RegistrationTestEvent).Assembly);

        using ServiceProvider provider = services.BuildServiceProvider();
        ArgumentOutOfRangeException failure = Should.Throw<ArgumentOutOfRangeException>(() =>
            provider.GetRequiredService<EventPayloadEvolutionRegistry>());

        failure.Message.ShouldContain("Invalid");
        failure.Message.ShouldContain("VersionTestEvent");
    }

    [Fact]
    public void AddEventStoreDomainEventHandler_IsIdempotentForSameEventAndHandler() {
        var services = new ServiceCollection();

        _ = services.AddEventStoreDomainEventHandler<RegistrationTestEvent, RegistrationTestHandler>();
        _ = services.AddEventStoreDomainEventHandler<RegistrationTestEvent, RegistrationTestHandler>();

        using ServiceProvider provider = services.BuildServiceProvider();
        IEventStoreDomainEventHandler<RegistrationTestEvent>[] handlers = provider
            .GetServices<IEventStoreDomainEventHandler<RegistrationTestEvent>>()
            .ToArray();

        handlers.Length.ShouldBe(1);
        _ = handlers[0].ShouldBeOfType<RegistrationTestHandler>();
    }
}
