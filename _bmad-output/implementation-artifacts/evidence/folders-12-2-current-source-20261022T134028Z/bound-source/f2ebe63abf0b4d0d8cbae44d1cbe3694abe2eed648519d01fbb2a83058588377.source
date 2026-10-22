using Hexalith.EventStore.PayloadProtection;
using Hexalith.EventStore.Server.Security;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hexalith.EventStore.Server.Configuration;

/// <summary>
/// Explicit registration of the fail-closed protection adapters and journal.
/// It passes no key delivery, profile, or anchor, and it is not called by the payload-protection opt-in, the AppHost, or the default host.
/// </summary>
public static class InteractionProtectionBindingServiceCollectionExtensions
{
    /// <summary>Registers the adapters and journal. Callers must not treat this registration as an installed custody binding.</summary>
    /// <param name="services">The host service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddInteractionProtectionBindings(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<IInteractionOccurrenceKeyProvider>(new InteractionOccurrenceKeyProvider(null));
        services.TryAddSingleton<IInteractionProtectionBindingProvider>(new InteractionProtectionBindingProvider(null));
        services.TryAddSingleton(new ProtectionCurrentAnchorJournal(null));
        services.TryAddSingleton<IInteractionOccurrenceAuthority>(static provider =>
            new InteractionOccurrenceAuthority(null, provider.GetRequiredService<ProtectionCurrentAnchorJournal>()));
        services.TryAddSingleton<IGovernanceGuardAuthority>(new GovernanceGuardAuthority(null));
        services.TryAddSingleton<IGuardedStateTransactionAuthority>(new GuardedStateTransactionAuthority(null));
        return services;
    }
}
