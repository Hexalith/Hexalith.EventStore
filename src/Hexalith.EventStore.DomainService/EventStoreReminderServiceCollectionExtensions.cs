using Dapr.Actors.Runtime;

using Hexalith.EventStore.Client.Effects;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Client.Registration;
using Hexalith.EventStore.Client.Reminders;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.DomainService;

/// <summary>
/// Registers the EventStore typed-reminder runtime (AD-20 R6): the reminder actor, the discovery index, the
/// periodic reconciler, the app-channel callback filter, and the <c>eventstore-reminders-unresolved</c>
/// readiness check. The domain module supplies only its <see cref="IReminderIntentSource"/>.
/// </summary>
/// <remarks>
/// Registration is presence-keyed: <c>UseEventStoreDomainService</c> maps the Dapr actor routes only when this
/// method was called. Submission needs an <see cref="ITrustedEffectSubmitter"/> and an
/// <see cref="IReminderDelegationTokenProvider"/> registered by the host; without either, due reminders fail
/// closed, stay retained, and readiness reports Degraded.
/// </remarks>
public static class EventStoreReminderServiceCollectionExtensions
{
    /// <summary>Registers the typed-reminder runtime with the domain's intent source.</summary>
    /// <typeparam name="TIntentSource">The domain-implemented intent source, registered as scoped.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">An optional delegate applied after <c>EventStore:Reminders</c> binding.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddEventStoreReminders<TIntentSource>(
        this IServiceCollection services,
        Action<EventStoreReminderOptions>? configure = null)
        where TIntentSource : class, IReminderIntentSource
    {
        ArgumentNullException.ThrowIfNull(services);

        OptionsBuilder<EventStoreReminderOptions> optionsBuilder = services.AddOptions<EventStoreReminderOptions>();
        if (services.Any(static descriptor => descriptor.ServiceType == typeof(ReminderIntentIndex)))
        {
            // Idempotent: a second call only layers its configuration delegate.
            if (configure is not null)
            {
                _ = optionsBuilder.Configure(configure);
            }

            return services;
        }

        _ = optionsBuilder.BindConfiguration(EventStoreReminderOptions.SectionName);
        if (configure is not null)
        {
            _ = optionsBuilder.Configure(configure);
        }

        _ = optionsBuilder
            .Validate(static options => options.Validate().Count == 0, "EventStore:Reminders options are invalid.")
            .ValidateOnStart();
        _ = services.AddSingleton<IPostConfigureOptions<EventStoreReminderOptions>>(serviceProvider =>
            new PostConfigureOptions<EventStoreReminderOptions>(Options.DefaultName, options =>
            {
                if (string.IsNullOrWhiteSpace(options.Workload))
                {
                    options.Workload = Environment.GetEnvironmentVariable("DAPR_APP_ID")
                        ?? serviceProvider.GetService<IHostEnvironment>()?.ApplicationName;
                }
            }));

        _ = services.AddEventStoreReadModelStore();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddScoped<IReminderIntentSource, TIntentSource>();
        services.TryAddSingleton<ReminderRuntimeStatus>();
        services.TryAddSingleton<ReminderIntentIndex>();
        services.TryAddScoped(static serviceProvider => new ReminderCoordinator(
            serviceProvider.GetRequiredService<IReminderIntentSource>(),
            serviceProvider.GetRequiredService<ReminderIntentIndex>(),
            serviceProvider.GetRequiredService<IReadModelStore>(),
            serviceProvider.GetRequiredService<IReadModelConditionalEraser>(),
            serviceProvider.GetRequiredService<IOptions<EventStoreReminderOptions>>(),
            serviceProvider.GetRequiredService<ReminderRuntimeStatus>(),
            serviceProvider.GetRequiredService<TimeProvider>(),
            serviceProvider.GetRequiredService<ILogger<ReminderCoordinator>>(),
            serviceProvider.GetService<ITrustedEffectSubmitter>(),
            serviceProvider.GetService<IReminderDelegationTokenProvider>()));
        services.TryAddSingleton<IReminderActorInvoker, DaprReminderActorInvoker>();
        services.TryAddSingleton<IReminderRegistrar, ReminderRegistrar>();
        services.TryAddSingleton<ReminderCallbackTokenFilter>();
        _ = services.AddSingleton<IStartupFilter, ReminderCallbackTokenStartupFilter>();
        services.TryAddSingleton<ReminderReconciler>();
        _ = services.AddHostedService(static serviceProvider => serviceProvider.GetRequiredService<ReminderReconciler>());
        _ = services.AddHealthChecks().Add(new HealthCheckRegistration(
            EventStoreDomainTelemetry.RemindersUnresolvedHealthCheckName,
            static serviceProvider => new ReminderReadinessHealthCheck(
                serviceProvider.GetRequiredService<ReminderRuntimeStatus>(),
                serviceProvider.GetRequiredService<ReminderCallbackTokenFilter>()),
            HealthStatus.Degraded,
            ["ready"]));

        // The actor type name is configuration, so it is registered when the actor runtime options resolve.
        services.AddActors(configure: null);
        _ = services.AddOptions<ActorRuntimeOptions>()
            .Configure<IOptions<EventStoreReminderOptions>>(static (actorOptions, reminderOptions) =>
            {
                string actorTypeName = reminderOptions.Value.ActorTypeName;
                if (!actorOptions.Actors.Any(registration => string.Equals(
                    registration.Type.ActorTypeName,
                    actorTypeName,
                    StringComparison.Ordinal)))
                {
                    actorOptions.Actors.RegisterActor<ReminderActor>(actorTypeName);
                }
            });

        return services;
    }
}
