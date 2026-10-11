using Hexalith.EventStore.Client.Processes;
using Hexalith.EventStore.Client.Registration;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Hexalith.EventStore.DomainService;

/// <summary>Registers the published checkpointed process and recovery runtime.</summary>
public static class EventStoreProcessServiceCollectionExtensions
{
    /// <summary>Registers domain supplied source discovery and pure translation with the hosted process runtime.</summary>
    /// <remarks>The host also supplies IProcessDelegationProvider, IProcessDispositionAuthorizer, IProcessDispositionAuditSink, ITrustedEffectSubmitter, and IEventStoreGatewayClient.</remarks>
    public static IServiceCollection AddEventStoreProcesses<TCatalog, TTranslator>(
        this IServiceCollection services,
        Action<EventStoreProcessOptions>? configure = null)
        where TCatalog : class, IProcessSourceCatalog
        where TTranslator : class, IProcessTranslator
    {
        ArgumentNullException.ThrowIfNull(services);
        ProcessRegistration? registration = services.FirstOrDefault(static service => service.ServiceType == typeof(ProcessRegistration))?.ImplementationInstance as ProcessRegistration;
        if (registration is not null)
        {
            if (registration.CatalogType != typeof(TCatalog) || registration.TranslatorType != typeof(TTranslator))
                throw new InvalidOperationException("Conflicting EventStore process adapter registration.");
            return services;
        }

        services.AddSingleton(new ProcessRegistration(typeof(TCatalog), typeof(TTranslator)));
        var options = services.AddOptions<EventStoreProcessOptions>();
        _ = options.BindConfiguration(EventStoreProcessOptions.SectionName);
        if (configure is not null) _ = options.Configure(configure);
        _ = options.Validate(static value => value.Validate().Count == 0, "EventStore:Processes options are invalid.").ValidateOnStart();
        _ = services.AddEventStoreReadModelStore();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddScoped<IProcessSourceCatalog, TCatalog>();
        services.TryAddScoped<IProcessTranslator, TTranslator>();
        services.TryAddSingleton<ProcessCheckpointStore>();
        services.TryAddSingleton<ProcessSweepStore>();
        services.TryAddScoped<CheckpointedProcessRunner>();
        services.TryAddScoped<ProcessDispositionService>();
        services.TryAddSingleton<ProcessReconciler>();
        _ = services.AddHostedService(static provider => provider.GetRequiredService<ProcessReconciler>());
        _ = services.AddHealthChecks().Add(new HealthCheckRegistration(
            "eventstore-processes-unresolved",
            static provider => new ProcessReadinessHealthCheck(
                provider.GetRequiredService<ProcessSweepStore>(),
                provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<EventStoreProcessOptions>>(),
                provider.GetRequiredService<TimeProvider>(),
                provider.GetRequiredService<ProcessReconciler>()),
            HealthStatus.Degraded,
            ["ready"]));
        return services;
    }
}
