using Hexalith.EventStore.Client.Registration;
using Hexalith.EventStore.ServiceDefaults.Routing;
using Hexalith.EventStore.Testing.CatalogInvocation;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Testing.OwnerSourceRead;

/// <summary>Explicit Debug/Development-only enrollment within the actual Tenants owner.</summary>
public static class OwnerSourceReadServiceCollectionExtensions
{
    /// <summary>Enrolls the finite private SDK helper only when expressly selected.</summary>
    /// <param name="services">Actual owner services.</param>
    /// <param name="configuration">Owner-owned configuration.</param>
    /// <param name="environment">Actual owner host environment.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddPrivateOwnerSourceRead(this IServiceCollection services,
        IConfiguration configuration, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);
        const string prefix = "EventStore:Testing:OwnerSourceRead";
        if (!configuration.GetValue<bool>(prefix + ":Enabled")) return services;
#if !DEBUG
        throw new InvalidOperationException("Private owner source reads require a Debug build.");
#else
        if (!environment.IsDevelopment() || configuration["Authentication:WorkloadIssuer:Workload"] != "tenants")
            throw new InvalidOperationException("Private owner source reads require the actual Development Tenants owner.");
        var selection = configuration.GetSection(prefix).Get<OwnerSourceReadOptions>()
            ?? throw new InvalidOperationException("Missing private owner source selection.");
        if (selection.Mode != OwnerSourceReadSelection.Mode || selection.GatewayAppId.Length is < 1 or > 64
            || selection.GatewayAppId.Any(character => !char.IsAsciiLetterLower(character) && !char.IsAsciiDigit(character) && character != '-'))
            throw new InvalidOperationException("Private owner source selection refused.");
        _ = new CatalogInvocationFiles(selection.Directory);
        services.AddHttpClient<OwnerSourceReadGateway>((provider, client) =>
        {
            var catalog = provider.GetRequiredService<IOptions<RouteCatalogHostOptions>>().Value;
            if (catalog is not { DevelopmentFixture: true, Profile: "Development", RequireTechnicalHttpBinding: true })
                throw new InvalidOperationException("Private owner source requires the real Development catalog.");
            client.BaseAddress = new Uri(RouteCatalogStateCoordinates.Capture(configuration, catalog).HttpEndpoint);
            client.Timeout = Timeout.InfiniteTimeSpan;
        })
        .AddEventStoreDaprServiceInvocation(selection.GatewayAppId, configuration["DAPR_API_TOKEN"])
        .AddTypedClient((http, provider) => new OwnerSourceReadGateway(http,
            provider.GetRequiredService<Hexalith.EventStore.ServiceDefaults.Authentication.IWorkloadAssertionIssuer>(),
            configuration, provider.GetRequiredService<TimeProvider>()));
        services.AddSingleton<IHostedService>(provider => new OwnerSourceReadWorker(selection, async (request, token) =>
        {
            using var scope = provider.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<OwnerSourceReadGateway>().ReadAsync(request, token).ConfigureAwait(false);
        }));
        return services;
#endif
    }
}
