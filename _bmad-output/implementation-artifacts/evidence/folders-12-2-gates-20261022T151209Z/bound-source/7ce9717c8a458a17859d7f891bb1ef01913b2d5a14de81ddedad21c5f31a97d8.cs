using Hexalith.EventStore.ServiceDefaults.Authentication;

using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.EventStore.DomainService;

/// <summary>
/// Wires a domain service's trusted-effect submission client with the service's own workload assertion (FR28).
/// </summary>
public static class EventStoreTrustedEffectSubmissionExtensions
{
    /// <summary>Gets the default workload audience of the EventStore gateway.</summary>
    public const string DefaultGatewayAudience = "eventstore";

    /// <summary>
    /// Attaches this domain service's short-lived workload assertion for <c>eventstore:trusted-effect</c> to every
    /// trusted-effect submission sent by the client. Call it before <c>AddEventStoreDaprServiceInvocation</c>, so the
    /// platform Dapr handler stays the innermost handler (AD-18).
    /// </summary>
    /// <remarks>
    /// The gateway admits the submission only when its <c>Authentication:DaprInternal:AllowedCallers</c> lists this
    /// domain service's workload identity. In symmetric mode the identity is <c>Authentication:WorkloadIssuer:Workload</c>
    /// (default <c>EventStore:DomainService:AppId</c>); in authority mode it is the <c>azp</c> of this service's own
    /// client registration (<c>Authentication:WorkloadIssuer:ClientId</c>/<c>ClientSecret</c>).
    /// </remarks>
    /// <param name="builder">The trusted-effect submitter's HTTP client builder.</param>
    /// <param name="gatewayAudience">The gateway workload audience (<c>Authentication:DaprInternal:Audience</c>).</param>
    /// <returns>The HTTP client builder.</returns>
    public static IHttpClientBuilder AddEventStoreTrustedEffectWorkloadAssertion(
        this IHttpClientBuilder builder,
        string gatewayAudience = DefaultGatewayAudience)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(gatewayAudience);
        _ = builder.Services.AddEventStoreWorkloadAssertionIssuer();
        return builder.AddHttpMessageHandler(serviceProvider =>
            new DomainServiceTrustedEffectAssertionHandler(serviceProvider, gatewayAudience.Trim()));
    }
}
