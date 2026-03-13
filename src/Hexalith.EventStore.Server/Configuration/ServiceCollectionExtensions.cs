
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Projections;
using Hexalith.EventStore.Server.Queries;
using Hexalith.EventStore.Contracts.Security;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Server.Configuration;
/// <summary>
/// DI registration extension methods for the EventStore Server components.
/// </summary>
public static class EventStoreServerServiceCollectionExtensions {
    /// <summary>
    /// Registers EventStore Server services including command routing and DAPR actor infrastructure.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The service collection for fluent chaining.</returns>
    public static IServiceCollection AddEventStoreServer(this IServiceCollection services, IConfiguration configuration) {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.TryAddSingleton<ICommandRouter, CommandRouter>();
        services.TryAddScoped<IQueryRouter, QueryRouter>();
        services.TryAddScoped<IETagService, DaprETagService>();
        services.TryAddSingleton<IDomainServiceResolver, DomainServiceResolver>();
        services.TryAddTransient<IDomainServiceInvoker, DaprDomainServiceInvoker>();
        services.TryAddSingleton<IEventPayloadProtectionService, NoOpEventPayloadProtectionService>();
        services.TryAddSingleton<ISnapshotManager, SnapshotManager>();
        services.TryAddSingleton<ITopicNameValidator, TopicNameValidator>();
        services.TryAddTransient<IEventPublisher, EventPublisher>();
        services.TryAddTransient<IDeadLetterPublisher, DeadLetterPublisher>();
        services.TryAddSingleton<IProjectionChangeNotifier, DaprProjectionChangeNotifier>();
        services.TryAddSingleton<IProjectionChangedBroadcaster, NoOpProjectionChangedBroadcaster>();
        services.TryAddSingleton<IValidateOptions<ProjectionChangeNotifierOptions>, ValidateProjectionChangeNotifierOptions>();
        _ = services.Configure<DomainServiceOptions>(configuration.GetSection("EventStore:DomainServices"));
        _ = services.AddOptions<ProjectionChangeNotifierOptions>()
            .Bind(configuration.GetSection("EventStore:ProjectionChanges"))
            .ValidateOnStart();
        _ = services.AddOptions<EventPublisherOptions>()
            .Bind(configuration.GetSection("EventStore:Publisher"));
        _ = services.AddOptions<EventDrainOptions>()
            .Bind(configuration.GetSection("EventStore:Drain"));
        _ = services.AddOptions<SnapshotOptions>()
            .Bind(configuration.GetSection("EventStore:Snapshots"))
            .Validate(o => { o.Validate(); return true; }, "Snapshot configuration is invalid. All intervals must be >= 10.")
            .ValidateOnStart();
        services.AddActors(options => {
            string? daprHttpPort = configuration["DAPR_HTTP_PORT"];
            if (!string.IsNullOrEmpty(daprHttpPort)) {
                options.HttpEndpoint = $"http://127.0.0.1:{daprHttpPort}";
            }

            options.Actors.RegisterActor<AggregateActor>();
            options.Actors.RegisterActor<ETagActor>();
        });

        return services;
    }
}
