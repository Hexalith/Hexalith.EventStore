using Hexalith.EventStore.Client.Registration;

namespace Hexalith.EventStore.Sample.BlazorUI.Services;

/// <summary>Registers the sample UI's authenticated SDK gateway path through its own Dapr sidecar.</summary>
public static class SampleUiGatewayServiceExtensions
{
    /// <summary>Uses the configured gateway app ID, preserving the historical default when absent.</summary>
    /// <param name="services">The UI service collection.</param>
    /// <param name="configuration">The UI host configuration.</param>
    /// <returns>The registered gateway HTTP client for host configuration.</returns>
    public static IHttpClientBuilder AddSampleUiGatewayClient(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        EventStoreApiAccessTokenProvider.AddHttpClient(services);
        services.AddSingleton<EventStoreApiAccessTokenProvider>();
        services.AddTransient<EventStoreApiAuthorizationHandler>();
        services.AddScoped<EventStoreProjectionQueryClient>();
        return services.AddEventStoreGatewayClient(options => options.BaseAddress = new Uri(DaprHttpEndpointResolver.Resolve(configuration)))
            .AddHttpMessageHandler<EventStoreApiAuthorizationHandler>()
            .AddEventStoreDaprServiceInvocation(configuration["EventStore:GatewayAppId"] ?? "eventstore", configuration["DAPR_API_TOKEN"]);
    }
}
