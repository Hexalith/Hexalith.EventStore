using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hexalith.EventStore.Server.Commands;

/// <summary>Opt-in registration for governed trusted effect source validation.</summary>
public static class TrustedEffectRetentionServiceCollectionExtensions
{
    /// <summary>
    /// Registers the source-evidence gate after the host supplies a retained-floor provider and
    /// a joint source/target retention policy. No default policy is installed.
    /// </summary>
    public static IServiceCollection AddEventStoreTrustedEffectRetention(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<ITrustedEffectRetentionGate, TrustedEffectRetentionGate>();
        return services;
    }
}
