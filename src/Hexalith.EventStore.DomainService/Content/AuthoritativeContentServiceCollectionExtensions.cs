using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hexalith.EventStore.DomainService.Content;

/// <summary>Registers EventStore's physical body capability with an explicit domain authority adapter.</summary>
public static class AuthoritativeContentServiceCollectionExtensions
{
    /// <summary>Registers the Dapr store; existing host DaprClient and current authority are required.</summary>
    public static IServiceCollection AddAuthoritativeContent<TAuthority>(this IServiceCollection services, Action<AuthoritativeContentOptions>? configure = null)
        where TAuthority : class, IContentAuthority
    {
        ArgumentNullException.ThrowIfNull(services);
        var options = services.AddOptions<AuthoritativeContentOptions>();
        if (configure is not null) _ = options.Configure(configure);
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddScoped<IContentAuthority, TAuthority>();
        services.TryAddScoped<IAuthoritativeContentStore, DaprAuthoritativeContentStore>();
        return services;
    }
}
