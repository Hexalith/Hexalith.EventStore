// Authority: fcf861aa3ba5d723ca589a8ac8a2a31c54a972fe96a28ec71db69e560ecc2394, sections 3-7; Core correction 360da00f91d3134054e4ec3f18c0b96921a36911a89d5935e7d59409634ca07e; base sections 5, 11, 15.
using Azure.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.EventStore.PayloadProtection.AzureKeyVault;
/// <summary>Explicitly registers only the companion backend; installation never replaces the Server protection service.</summary>
public static class AzureKeyVaultServiceCollectionExtensions
{
    /// <summary>Registers the closed provider subsection and explicit capability probe, using deterministic managed identity in every environment.</summary>
    /// <param name = "services">The host service collection.</param>
    /// <param name = "configuration">The EventStore:PayloadProtection:AzureKeyVault subsection.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddAzureKeyVaultPayloadProtectionBackend(this IServiceCollection services, IConfigurationSection configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        if (services.Any(descriptor => descriptor.ServiceType == typeof(PayloadProtectionBackend)))
        {
            throw AzureKeyVaultOptionsValidator.Invalid();
        }

        AzureKeyVaultPayloadProtectionOptions options = AzureKeyVaultOptionsValidator.Load(configuration);
        services.AddSingleton(options);
        services.AddSingleton(_ => new AzureKeyVaultSafeTransport(options));
        services.AddSingleton<TokenCredential>(_ => AzureKeyVaultCredentialFactory.Create(options));
        services.AddSingleton(provider => new AzureKeyVaultMetadataClient(options, provider.GetRequiredService<TokenCredential>(), provider.GetRequiredService<AzureKeyVaultSafeTransport>()));
        services.AddSingleton(provider => new AzureKeyVaultDataPlane(options, provider.GetRequiredService<TokenCredential>(), provider.GetRequiredService<AzureKeyVaultSafeTransport>()));
        services.AddSingleton<AzureKeyVaultPayloadProtectionBackend>();
        services.AddSingleton<PayloadProtectionBackend>(provider => provider.GetRequiredService<AzureKeyVaultPayloadProtectionBackend>());
        services.AddSingleton<AzureKeyVaultReadinessProbe>();
        return services;
    }
}
