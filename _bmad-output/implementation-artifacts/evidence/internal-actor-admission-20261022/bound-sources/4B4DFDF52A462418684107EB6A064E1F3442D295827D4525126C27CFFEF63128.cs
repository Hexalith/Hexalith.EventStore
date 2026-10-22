using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.ServiceDefaults.Authentication;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hexalith.EventStore.DomainService;

/// <summary>Composes the SDK gateway client's workload command and stream transport for a domain host.</summary>
public static class EventStoreGatewayWorkloadTransportExtensions
{
    /// <summary>
    /// Uses workload-only command and stream endpoints with assertions from the host's independently configured
    /// issuer. Each assertion grants exactly one operation, receiving audience, tenant and configured domain.
    /// </summary>
    /// <remarks>
    /// Register one domain per gateway client composition. Authority modes unable to issue resource-bound
    /// assertions fail closed; neither a forwarded human token nor a Dapr attribution header substitutes for one.
    /// </remarks>
    /// <param name="builder">The registered EventStore gateway client.</param>
    /// <param name="domain">The domain this host is authorized to access.</param>
    /// <param name="gatewayAudience">The gateway's receiving workload audience.</param>
    /// <returns>The client builder.</returns>
    public static IHttpClientBuilder AddEventStoreGatewayWorkloadTransport(
        this IHttpClientBuilder builder,
        string domain,
        string gatewayAudience = EventStoreTrustedEffectSubmissionExtensions.DefaultGatewayAudience)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        ArgumentException.ThrowIfNullOrWhiteSpace(gatewayAudience);
        _ = builder.Services.AddEventStoreWorkloadAssertionIssuer();
        _ = builder.Services.Configure<EventStoreGatewayClientOptions>(static options => options.UseWorkloadTransport = true);
        builder.Services.TryAddSingleton<IEventStoreGatewayWorkloadAssertionSource>(services =>
            new DomainServiceGatewayWorkloadAssertionSource(
                services.GetRequiredService<IWorkloadAssertionIssuer>(), domain.Trim(), gatewayAudience.Trim()));
        return builder;
    }
}
