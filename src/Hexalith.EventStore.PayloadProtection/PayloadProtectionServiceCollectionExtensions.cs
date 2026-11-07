// Approved on-premises supplement: 43fc3400f9e01662eac5ad945407711d0d24a744685e92cef203c2162106e2d3; sections 1-7.
// Base authority: de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e, sections 5.2-5.3; on-premises supplement sections 1-2, 6-7.
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>Registers explicit closed options and startup checks; backend qualification and capacity gates precede any Server activation.</summary>
public static class PayloadProtectionServiceCollectionExtensions
{
    /// <summary>Preserves Server's concrete no-op when disabled and refuses enabled startup until the separately gated host integration exists.</summary>
    /// <param name="services">The host service collection.</param>
    /// <param name="configuration">The exact EventStore:PayloadProtection section.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddEventStorePayloadProtection(this IServiceCollection services, IConfigurationSection configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        if (services.Any(d => d.ServiceType == typeof(PayloadProtectionOptions))) { throw new InvalidOperationException("Payload protection registration is duplicated."); }
        PayloadProtectionOptions options = PayloadProtectionOptions.Load(configuration);
        // Refuse before any hosted service or web listener can start, including concurrent host startup.
        if (options.Enabled) { throw new InvalidOperationException("Payload protection startup admission is unavailable."); }
        services.AddSingleton(options);
        services.AddSingleton(options.AuthorityProfile);
        services.AddSingleton<IHostedService>(provider => new PayloadProtectionStartupValidator(provider, options, services));
        return services;
    }
}
