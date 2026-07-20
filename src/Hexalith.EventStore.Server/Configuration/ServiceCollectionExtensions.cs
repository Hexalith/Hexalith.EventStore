
using Dapr.Client;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Client.Registration;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Projections;
using Hexalith.EventStore.Server.Queries;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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

        _ = services.AddEventStoreReadModelStore();
        services.TryAddSingleton<ICommandRouter, CommandRouter>();
        services.TryAddSingleton<IIdempotencyDigestKeyProvider>(static serviceProvider =>
        {
            IOptions<IdempotencyAdmissionOptions> options = serviceProvider
                .GetRequiredService<IOptions<IdempotencyAdmissionOptions>>();
            return options.Value.DigestKeySource == IdempotencyDigestKeySource.DaprSecret
                ? ActivatorUtilities.CreateInstance<DaprSecretIdempotencyDigestKeyProvider>(serviceProvider)
                : new ConfigurationIdempotencyDigestKeyProvider(options);
        });
        services.TryAddSingleton<IdempotencyKeyProtector>();
        services.TryAddSingleton<IdempotencyExecutionContextProtector>();
        services.TryAddSingleton<CanonicalIdempotencyIntentEncoder>();
        services.TryAddSingleton<IIdempotencyIntentAdapterRegistry, IdempotencyIntentAdapterRegistry>();
        services.TryAddSingleton<IIdempotencyAdmissionCoordinator, IdempotencyAdmissionCoordinator>();
        services.TryAddSingleton<IdempotencyTenantLifecyclePurger>();
        services.TryAddScoped<IQueryRouter, QueryRouter>();
        services.TryAddScoped<IETagService, DaprETagService>();
        services.TryAddSingleton<IDomainServiceResolver, DomainServiceResolver>();
        services.TryAddTransient<IDomainServiceInvoker, DaprDomainServiceInvoker>();
        services.TryAddTransient<IAggregateStateReconstructor, DaprAggregateStateReconstructor>();
        _ = services.AddHttpClient();
        services.TryAddSingleton<IEventPayloadProtectionService, NoOpEventPayloadProtectionService>();
        services.TryAddSingleton<IGlobalPositionAllocator, DaprGlobalPositionAllocator>();
        services.TryAddSingleton<ISnapshotManager, SnapshotManager>();
        services.TryAddSingleton<ITopicNameValidator, TopicNameValidator>();
        services.TryAddTransient<IProjectionUpdateOrchestrator, ProjectionUpdateOrchestrator>();
        services.TryAddTransient<IProjectionPollerDeliveryGateway, ProjectionUpdateOrchestrator>();
        services.TryAddTransient<IProjectionRebuildOrchestrator, ProjectionUpdateOrchestrator>();
        services.TryAddTransient<INamedProjectionDispatchCoordinator, NamedProjectionDispatchCoordinator>();
        services.TryAddSingleton<IProjectionDeliveryRetryScheduler, DaprProjectionDeliveryRetryScheduler>();
        services.TryAddSingleton<IProjectionActivationOutbox, DaprProjectionActivationOutbox>();
        services.TryAddSingleton<NamedProjectionRouteCatalog>();
        services.TryAddSingleton<INamedProjectionRouteCatalog>(static sp => sp.GetRequiredService<NamedProjectionRouteCatalog>());
        services.TryAddTransient<IEventPublisher, EventPublisher>();
        services.TryAddTransient<IDeadLetterPublisher, DeadLetterPublisher>();
        services.TryAddSingleton<IProjectionChangeNotifier, DaprProjectionChangeNotifier>();
        services.TryAddSingleton<IProjectionChangedBroadcaster, NoOpProjectionChangedBroadcaster>();
        services.TryAddSingleton<IProjectionCheckpointTracker, ProjectionCheckpointTracker>();
        services.TryAddSingleton<IProjectionCheckpointEraser>(static sp =>
            (IProjectionCheckpointEraser)sp.GetRequiredService<IProjectionCheckpointTracker>());
        services.TryAddSingleton<IProjectionDeliveryCheckpointStore>(static sp =>
            (IProjectionDeliveryCheckpointStore)sp.GetRequiredService<IProjectionCheckpointTracker>());
        services.TryAddSingleton<IProjectionDeliveryStateStore, DaprProjectionDeliveryStateStore>();
        services.TryAddSingleton<IProjectionDeliveryIdempotencyCoordinator, ProjectionDeliveryIdempotencyCoordinator>();
        services.TryAddSingleton<IProjectionDeliveryHistoryReader, EventStoreProjectionDeliveryHistoryReader>();
        services.TryAddSingleton<IProjectionDeliveryReconciler, ProjectionDeliveryReconciler>();
        services.TryAddSingleton<IProjectionDeliveryCutover, ProjectionDeliveryCutover>();
        services.TryAddSingleton<IProjectionLifecycleGateway, DaprProjectionLifecycleGateway>();
        services.TryAddSingleton<IProjectionRebuildWriteGateway, DaprProjectionRebuildWriteGateway>();
        services.TryAddSingleton<IProjectionSlotRegistry>(ProjectionSlotServiceCollectionExtensions.BuildSlotRegistry);
        services.TryAddSingleton<IProjectionReadModelAddressFactory, ProjectionReadModelAddressFactory>();
        services.TryAddSingleton<IProjectionRebuildCheckpointStore, ProjectionRebuildCheckpointStore>();
        services.TryAddSingleton<IProjectionRebuildCheckpointEraser>(static sp =>
            (IProjectionRebuildCheckpointEraser)sp.GetRequiredService<IProjectionRebuildCheckpointStore>());
        services.TryAddSingleton<IProjectionEraseCoordinator, ProjectionEraseCoordinator>();
        services.TryAddSingleton<IProjectionPollerTickSource, PeriodicProjectionPollerTickSource>();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IValidateOptions<IdempotencyRetentionOptions>, ValidateIdempotencyRetentionOptions>();
        _ = services.AddOptions<IdempotencyRetentionOptions>()
            .Bind(configuration.GetSection("EventStore:Idempotency"))
            .ValidateOnStart();
        services.TryAddSingleton<IValidateOptions<IdempotencyAdmissionOptions>, ValidateIdempotencyAdmissionOptions>();
        _ = services.AddOptions<IdempotencyAdmissionOptions>()
            .Bind(configuration.GetSection("EventStore:IdempotencyAdmission"))
            .ValidateOnStart();
        services.TryAddSingleton<IValidateOptions<ProjectionChangeNotifierOptions>, ValidateProjectionChangeNotifierOptions>();
        _ = services.AddOptions<DomainServiceOptions>()
            .Bind(configuration.GetSection("EventStore:DomainServices"))
            .Validate(
                static options => options.InvocationTimeoutSeconds > 0
                    && options.InvocationTimeoutSeconds <= DomainServiceOptions.MaximumInvocationTimeoutSeconds,
                $"{nameof(DomainServiceOptions.InvocationTimeoutSeconds)} must be between 1 and "
                    + $"{DomainServiceOptions.MaximumInvocationTimeoutSeconds} seconds.")
            .ValidateOnStart();
        _ = services.AddOptions<ProjectionChangeNotifierOptions>()
            .Bind(configuration.GetSection("EventStore:ProjectionChanges"))
            .ValidateOnStart();
        _ = services.AddOptions<EventPublisherOptions>()
            .Bind(configuration.GetSection("EventStore:Publisher"));
        _ = services.AddOptions<EventDrainOptions>()
            .Bind(configuration.GetSection("EventStore:Drain"));
        services.TryAddSingleton<IValidateOptions<BackpressureOptions>, ValidateBackpressureOptions>();
        _ = services.AddOptions<BackpressureOptions>()
            .Bind(configuration.GetSection("EventStore:Backpressure"))
            .ValidateOnStart();
        services.TryAddSingleton<IValidateOptions<CommandConcurrencyOptions>, ValidateCommandConcurrencyOptions>();
        _ = services.AddOptions<CommandConcurrencyOptions>()
            .Bind(configuration.GetSection("EventStore:CommandConcurrency"))
            .ValidateOnStart();
        _ = services.AddOptions<EventStoreActorOptions>()
            .Bind(configuration.GetSection("EventStore:Actors"))
            .Validate(o => !string.IsNullOrWhiteSpace(o.AggregateActorTypeName), "Aggregate actor type name must be configured.")
            .ValidateOnStart();
        _ = services.AddOptions<SnapshotOptions>()
            .Bind(configuration.GetSection("EventStore:Snapshots"))
            .Validate(o => { o.Validate(); return true; }, "Snapshot configuration is invalid. All intervals must be >= 10.")
            .ValidateOnStart();
        _ = services.AddOptions<ProjectionOptions>()
            .Bind(configuration.GetSection("EventStore:Projections"))
            .Validate(o => { o.Validate(); return true; }, "Projection configuration is invalid. All intervals must be >= 0 and domain keys must be non-empty.")
            .ValidateOnStart();
        _ = services.AddOptions<ProjectionDispatchOptions>()
            .Bind(configuration.GetSection("EventStore:ProjectionDispatch"))
            .Validate(o => { o.Validate(); return true; }, "Projection dispatch configuration is invalid.")
            .ValidateOnStart();
        _ = services.AddOptions<ProjectionDeliveryIdempotencyOptions>()
            .Bind(configuration.GetSection("EventStore:ProjectionDeliveryIdempotency"))
            .Validate(o => { o.Validate(); return true; }, "Projection delivery idempotency configuration is invalid.")
            .ValidateOnStart();
        _ = services.AddHealthChecks().AddCheck<ProjectionDeliveryWriterProtocolHealthCheck>(
            "projection-delivery-writer-protocol",
            failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy,
            tags: ["ready"]);
        _ = services.AddHostedService<ProjectionDiscoveryHostedService>();
        _ = services.AddSingleton<IHostedService>(serviceProvider =>
            serviceProvider.GetService<DaprClient>() is null
                ? NoOpHostedService.Instance
                : ActivatorUtilities.CreateInstance<ProjectionDeliveryRetryWorker>(serviceProvider));
        _ = services.AddSingleton<IHostedService>(serviceProvider =>
            serviceProvider.GetService<DaprClient>() is null
                ? NoOpHostedService.Instance
                : ActivatorUtilities.CreateInstance<ProjectionActivationWorker>(serviceProvider));
        // P-DEC1-8P/P4-9P: register cleanup only when DaprClient is available. Non-DAPR test
        // hosts use the same no-op pattern as the projection poller so they do not emit recurring
        // sweep failures from DAPR-backed stores.
        _ = services.AddSingleton<IHostedService>(serviceProvider =>
            serviceProvider.GetService<DaprClient>() is null
                ? NoOpHostedService.Instance
                : ActivatorUtilities.CreateInstance<ActiveRebuildIndexCleanupService>(serviceProvider));
        _ = services.AddSingleton<IHostedService>(serviceProvider => {
            if (serviceProvider.GetService<DaprClient>() is null) {
                // R2P9 — emit the disable warning via a source-generated LoggerMessage so the Stage tag
                // surfaces as a structured log property (consistent with the other ProjectionPolling* logs).
                // R2P10 — resolve the factory via GetService rather than the typed logger, then fall back
                // to Console.Error so a misconfigured logging stack cannot silence this operator alert
                // (the whole point of this branch is to surface that polling is silently disabled).
                ILoggerFactory? loggerFactory = serviceProvider.GetService<ILoggerFactory>();
                if (loggerFactory is not null) {
                    ILogger<ProjectionPollerService> logger = loggerFactory.CreateLogger<ProjectionPollerService>();
                    ProjectionPollerDisabledLog.PollerDisabled(logger);
                }
                else {
                    Console.Error.WriteLine(
                        "[Stage=ProjectionPollerDisabled] DaprClient is not registered; projection polling is disabled. "
                        + "Configured polling-mode domains will not deliver projections automatically until DaprClient is added to the container.");
                }

                return NoOpHostedService.Instance;
            }

            return ActivatorUtilities.CreateInstance<ProjectionPollerService>(serviceProvider);
        });
        services.AddActors(options => {
            string? daprHttpPort = configuration["DAPR_HTTP_PORT"];
            if (!string.IsNullOrEmpty(daprHttpPort)) {
                options.HttpEndpoint = $"http://127.0.0.1:{daprHttpPort}";
            }

            string? aggregateActorTypeName = configuration["EventStore:Actors:AggregateActorTypeName"];
            options.Actors.RegisterActor<AggregateActor>(
                string.IsNullOrWhiteSpace(aggregateActorTypeName)
                    ? nameof(AggregateActor)
                    : aggregateActorTypeName);
            options.Actors.RegisterActor<IdempotencyAdmissionActor>(IdempotencyAdmissionActor.ActorTypeName);
            options.Actors.RegisterActor<IdempotencyAdmissionDirectoryActor>(IdempotencyAdmissionDirectoryActor.ActorTypeName);
            options.Actors.RegisterActor<IdempotencyTenantLifecycleActor>(IdempotencyTenantLifecycleActor.ActorTypeName);
            options.Actors.RegisterActor<IdempotencyLegacyInventoryActor>(IdempotencyLegacyInventoryActor.ActorTypeName);
            options.Actors.RegisterActor<ETagActor>();
            options.Actors.RegisterActor<GlobalPositionActor>(GlobalPositionActor.ActorTypeName);
            options.Actors.RegisterActor<EventReplayProjectionActor>(QueryRouter.ProjectionActorTypeName);
            options.Actors.RegisterActor<ProjectionLifecycleActor>(ProjectionLifecycleActor.ActorTypeName);
        });

        return services;
    }
}

internal sealed class NoOpHostedService : IHostedService {
    public static NoOpHostedService Instance { get; } = new();

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

internal static partial class ProjectionPollerDisabledLog {
    [LoggerMessage(
        EventId = 1140,
        Level = LogLevel.Warning,
        Message = "DaprClient is not registered; projection polling is disabled. Configured polling-mode domains will not deliver projections automatically until DaprClient is added to the container. Stage=ProjectionPollerDisabled")]
    public static partial void PollerDisabled(ILogger logger);
}
