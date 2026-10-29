using Dapr.Actors.Runtime;

using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.PayloadProtection;
using Hexalith.EventStore.Server.Security;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hexalith.EventStore.Server.Configuration;

/// <summary>Explicit infrastructure opt-in to the occurrence-authenticated content engine.</summary>
public static class InteractionPayloadProtectionServiceCollectionExtensions
{
    /// <summary>Installs the production engine with every host-owned key, binding, manifest and authority port explicitly supplied. No adapter is inferred from the no-op defaults.</summary>
    /// <param name="services">Infrastructure services.</param>
    /// <param name="registry">Authenticated occurrence registry.</param>
    /// <param name="keys">Current privately custodied root and digest keys.</param>
    /// <param name="bindings">Independent occurrence/writer binding authority.</param>
    /// <param name="manifests">Qualified all-or-none physical manifest provider.</param>
    /// <param name="occurrences">Independent registry original/writer/nonrollback authority.</param>
    /// <param name="consumption">Independent current dispatch, block, compromise and owner-state authority.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddInteractionPayloadProtection(this IServiceCollection services,
        IInteractionOccurrenceRegistry registry, IInteractionOccurrenceKeyProvider keys,
        IInteractionProtectionBindingProvider bindings, IAtomicDeletionManifestProvider manifests,
        IInteractionOccurrenceAuthority occurrences, IDeletionConsumptionAuthority consumption)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(keys);
        ArgumentNullException.ThrowIfNull(bindings);
        ArgumentNullException.ThrowIfNull(manifests);
        ArgumentNullException.ThrowIfNull(occurrences);
        ArgumentNullException.ThrowIfNull(consumption);
        services.Replace(ServiceDescriptor.Singleton(registry));
        services.Replace(ServiceDescriptor.Singleton(keys));
        services.Replace(ServiceDescriptor.Singleton(bindings));
        services.Replace(ServiceDescriptor.Singleton(manifests));
        services.Replace(ServiceDescriptor.Singleton(occurrences));
        services.Replace(ServiceDescriptor.Singleton(consumption));
        return services.AddInteractionPayloadProtection();
    }

    /// <summary>
    /// Replaces the no-op hook with the actual sealed engine and registers the occurrence-registry and deletion-consumption actors.
    /// The host must independently install key, binding, manifest, and authority services. This opt-in does not create them.
    /// </summary>
    /// <param name="services">The host's infrastructure service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddInteractionPayloadProtection(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<InteractionPayloadProtectionService>();
        services.Replace(ServiceDescriptor.Singleton<IEventPayloadProtectionService>(
            static provider => provider.GetRequiredService<InteractionPayloadProtectionService>()));
        services.AddOptions<ActorRuntimeOptions>().PostConfigure(RegisterProtectionActors);
        return services;
    }

    private static void RegisterProtectionActors(ActorRuntimeOptions options)
    {
        RegisterActor<InteractionOccurrenceRegistryActor>(options, InteractionOccurrenceRegistryActor.ActorTypeName);
        RegisterActor<DeletionConsumptionActor>(options, DeletionConsumptionActor.ActorTypeName);
    }

    private static void RegisterActor<TActor>(ActorRuntimeOptions options, string actorTypeName)
        where TActor : Actor
    {
        if (options.Actors.Any(registration => string.Equals(registration.Type.ActorTypeName, actorTypeName, StringComparison.Ordinal)))
        {
            return;
        }

        options.Actors.RegisterActor<TActor>(actorTypeName);
    }
}
