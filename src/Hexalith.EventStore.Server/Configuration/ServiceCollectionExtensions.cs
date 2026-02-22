
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Events;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hexalith.EventStore.Server.Configuration;
/// <summary>
/// DI registration extension methods for the EventStore Server components.
/// </summary>
public static class EventStoreServerServiceCollectionExtensions {
    /// <summary>
    /// Registers EventStore Server services including command routing and DAPR actor infrastructure.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for fluent chaining.</returns>
    public static IServiceCollection AddEventStoreServer(this IServiceCollection services, IConfiguration configuration) {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.TryAddSingleton<ICommandRouter, CommandRouter>();
        services.TryAddSingleton<IDomainServiceResolver, DomainServiceResolver>();
        services.TryAddTransient<IDomainServiceInvoker, DaprDomainServiceInvoker>();
        services.TryAddSingleton<ISnapshotManager, SnapshotManager>();
        services.TryAddSingleton<ITopicNameValidator, TopicNameValidator>();
        services.TryAddTransient<IEventPublisher, EventPublisher>();
        services.TryAddTransient<IDeadLetterPublisher, DeadLetterPublisher>();
        _ = services.Configure<DomainServiceOptions>(configuration.GetSection("EventStore:DomainServices"));
        _ = services.AddOptions<EventPublisherOptions>()
            .Bind(configuration.GetSection("EventStore:Publisher"));
        _ = services.AddOptions<EventDrainOptions>()
            .Bind(configuration.GetSection("EventStore:Drain"));
        _ = services.AddOptions<SnapshotOptions>()
            .Bind(configuration.GetSection("EventStore:Snapshots"))
            .Validate(o => { o.Validate(); return true; }, "Snapshot configuration is invalid. All intervals must be >= 10.")
            .ValidateOnStart();
        services.AddActors(options => options.Actors.RegisterActor<AggregateActor>());

        return services;
    }
}
