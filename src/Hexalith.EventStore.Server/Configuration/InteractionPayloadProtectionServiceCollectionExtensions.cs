using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.PayloadProtection;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hexalith.EventStore.Server.Configuration;

/// <summary>Explicit infrastructure opt-in to the occurrence-authenticated content engine.</summary>
public static class InteractionPayloadProtectionServiceCollectionExtensions
{
    /// <summary>Replaces the no-op hook with the actual sealed engine. The host must independently install registry, custody and binding authorities; missing services fail resolution.</summary>
    /// <param name="services">The host's infrastructure service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddInteractionPayloadProtection(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<InteractionPayloadProtectionService>();
        services.Replace(ServiceDescriptor.Singleton<IEventPayloadProtectionService>(
            static provider => provider.GetRequiredService<InteractionPayloadProtectionService>()));
        return services;
    }
}
