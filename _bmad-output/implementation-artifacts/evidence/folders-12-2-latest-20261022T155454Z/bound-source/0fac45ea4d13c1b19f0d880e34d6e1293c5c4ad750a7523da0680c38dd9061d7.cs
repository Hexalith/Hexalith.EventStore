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
