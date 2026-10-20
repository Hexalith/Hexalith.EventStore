using Hexalith.EventStore.Client.Streams;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hexalith.EventStore.Client.Registration;

/// <summary>Explicit opt-in to metadata-only source inspection using the existing private actor transport.</summary>
public static class NativeSourceMetadataServiceCollectionExtensions
{
    /// <summary>Registers the shared adapter without granting resource access or certifying cold activation.</summary>
    public static IServiceCollection AddEventStoreNativeSourceMetadataReader(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton(TimeProvider.System);
        services.AddHttpClient<INativeSourceMetadataReader, DaprNativeSourceMetadataReader>();
        return services;
    }
}
