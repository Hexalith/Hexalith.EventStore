using Hexalith.EventStore.Client.Gateway;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hexalith.EventStore.DomainService;

/// <summary>Composes authority-issued access tokens for the gateway's ordinary protected endpoints.</summary>
public static class EventStoreGatewayAuthorityTransportExtensions
{
    /// <summary>Uses configured authority credentials for ordinary command and stream requests.</summary>
    /// <remarks>The receiving gateway retains its normal JWT validation and authorization policies.</remarks>
    /// <param name="builder">The registered gateway client.</param>
    /// <param name="configuration">The host configuration containing EventStore:Authentication.</param>
    /// <returns>The client builder.</returns>
    public static IHttpClientBuilder AddEventStoreGatewayAuthorityTransport(this IHttpClientBuilder builder, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configuration);
        _ = builder.Services.Configure<EventStoreGatewayClientOptions>(static options => options.UseWorkloadTransport = false);
        _ = builder.Services.AddHttpClient(GatewayAuthorityAccessTokenProvider.TokenClientName, static client => client.Timeout = TimeSpan.FromSeconds(15))
            .ConfigurePrimaryHttpMessageHandler(static () => new HttpClientHandler { AllowAutoRedirect = false });
        builder.Services.TryAddSingleton(services => new GatewayAuthorityAccessTokenProvider(configuration,
            services.GetRequiredService<IHttpClientFactory>(), services.GetService<Microsoft.Extensions.Hosting.IHostEnvironment>()));
        builder.Services.TryAddTransient<GatewayAuthorityAccessTokenHandler>();
        return builder.AddHttpMessageHandler<GatewayAuthorityAccessTokenHandler>();
    }
}
