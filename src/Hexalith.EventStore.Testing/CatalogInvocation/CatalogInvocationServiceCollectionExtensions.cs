using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Testing.CatalogInvocation;

/// <summary>Explicit local Debug/Development-only activation, absent from normal executable references.</summary>
public static class CatalogInvocationServiceCollectionExtensions
{
    /// <summary>Enrolls a private worker only when the exact profile is explicitly enabled.</summary>
    /// <param name="services">Actual gateway services.</param>
    /// <param name="configuration">Host-owned configuration.</param>
    /// <param name="environment">Actual host environment.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddPrivateCatalogInvocation(this IServiceCollection services,
        IConfiguration configuration, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);
        const string prefix = "EventStore:Testing:CatalogInvocation";
        if (!configuration.GetValue<bool>(prefix + ":Enabled")) return services;
#if !DEBUG
        throw new InvalidOperationException("Private catalog invocation requires a Debug build.");
#else
        if (!environment.IsDevelopment()) throw new InvalidOperationException("Private catalog invocation requires Development.");
        CatalogInvocationOptions selection = configuration.GetSection(prefix).Get<CatalogInvocationOptions>()
            ?? throw new InvalidOperationException("Missing private invocation selection.");
        static bool AppId(string value) => value.Length is > 0 and <= 64
            && value.All(character => char.IsAsciiLetterLower(character) || char.IsAsciiDigit(character) || character == '-');
        if (selection.MetadataAppIds.Length is < 1 or > 8 || selection.MetadataAppIds.Any(value => !AppId(value))
            || selection.MetadataAppIds.Distinct(StringComparer.Ordinal).Count() != selection.MetadataAppIds.Length
            || !AppId(selection.ProjectionAppId) || !selection.MetadataAppIds.Contains(selection.ProjectionAppId, StringComparer.Ordinal))
            throw new InvalidOperationException("Private invocation requires finite selected owners.");
        _ = new CatalogInvocationFiles(selection.Directory);
        string endpointText = configuration["DAPR_HTTP_ENDPOINT"] ?? "http://127.0.0.1:" + configuration["DAPR_HTTP_PORT"];
        if (!Uri.TryCreate(endpointText, UriKind.Absolute, out Uri? endpoint) || !endpoint.IsLoopback
            || endpoint.Scheme != "http" || endpoint.AbsolutePath != "/" || endpoint.UserInfo.Length != 0
            || endpoint.Query.Length != 0 || endpoint.Fragment.Length != 0 || endpoint.Port <= 0)
            throw new InvalidOperationException("The private worker requires the actual local Dapr HTTP endpoint.");
        services.AddSingleton<IHostedService>(provider => new CatalogInvocationWorker(selection,
            provider.GetRequiredService<IRouteCatalogAuthority>(), provider.GetRequiredService<IOptions<RouteCatalogHostOptions>>(),
            provider.GetRequiredService<IWorkloadAssertionIssuer>(), endpoint));
        return services;
#endif
    }
}
