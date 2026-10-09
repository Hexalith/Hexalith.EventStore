using Hexalith.EventStore.ServiceDefaults.Authentication;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.DomainService;

/// <summary>
/// Registers the internal trust boundary of a domain-service host (FR28, AD-10, AD-16, AD-28).
/// </summary>
/// <remarks>
/// <para>
/// Every canonical operational route requires the Dapr application-channel token plus a short-lived workload
/// assertion from the trusted JWT issuer whose caller, audience (this service's Dapr application id), and granted
/// operation match the route. Sidecar-originated deliveries (pub/sub subscriptions, subscription discovery, actor
/// runtime calls) require the application-channel token. Every other endpoint falls back to the any-workload policy.
/// Only <c>/health</c>, <c>/alive</c>, and <c>/ready</c> stay anonymous.
/// </para>
/// <para>
/// Configuration: <c>APP_API_TOKEN</c>, the shared JWT contract under <c>Authentication:JwtBearer</c>, and
/// <c>Authentication:Workload</c> (<c>Audience</c> defaults to <c>EventStore:DomainService:AppId</c>;
/// <c>AllowedCallers</c> defaults to <c>eventstore</c>).
/// </para>
/// <para>
/// The host also receives the outbound workload-assertion issuer (<c>Authentication:WorkloadIssuer</c>, whose
/// <c>Workload</c> defaults to <c>EventStore:DomainService:AppId</c>), used when it submits trusted effects through a
/// client configured with <see cref="EventStoreTrustedEffectSubmissionExtensions.AddEventStoreTrustedEffectWorkloadAssertion"/>.
/// </para>
/// </remarks>
public static class EventStoreDomainServiceSecurityExtensions
{
    /// <summary>Gets the configuration section holding the domain-service workload settings.</summary>
    public const string WorkloadSection = "Authentication:Workload";

    /// <summary>Gets the default workload allowed to call domain-service routes.</summary>
    public const string DefaultCaller = "eventstore";

    /// <summary>
    /// Adds the domain-service trust boundary. Called by <c>AddEventStoreDomainService</c>; idempotent.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddEventStoreDomainServiceSecurity(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (services.Any(static descriptor => descriptor.ServiceType == typeof(EventStoreDomainServiceEndpointSource)))
        {
            return services;
        }

        _ = services.AddSingleton<EventStoreDomainServiceEndpointSource>();
        _ = services
            .AddAuthentication()
            .AddEventStoreWorkloadScheme(
                EventStoreWorkloadAuthenticationDefaults.WorkloadScheme,
                WorkloadSection,
                static (options, serviceProvider) =>
                {
                    if (string.IsNullOrWhiteSpace(options.Audience))
                    {
                        options.Audience = serviceProvider.GetService<IOptions<DomainProjectionIdentityOptions>>()?.Value.AppId;
                    }

                    if (options.AllowedCallers.Count == 0)
                    {
                        options.AllowedCallers.Add(DefaultCaller);
                    }
                })
            .AddEventStoreSidecarChannelScheme();
        _ = services.AddEventStoreWorkloadPolicies(
            EventStoreWorkloadAuthenticationDefaults.WorkloadScheme,
            EventStoreDomainServiceRoutes.Operations);
        _ = services.Configure<AuthorizationOptions>(static options =>
            options.FallbackPolicy ??= EventStoreWorkloadAuthenticationExtensions.CreateAnyWorkloadPolicy(
                EventStoreWorkloadAuthenticationDefaults.WorkloadScheme));

        // A domain service is itself an internal caller when it submits trusted effects to EventStore: it proves its
        // own workload identity (its Dapr application id) with an assertion from the same trusted issuer.
        _ = services.AddEventStoreWorkloadAssertionIssuer();
        _ = services.AddOptions<WorkloadAssertionIssuerOptions>()
            .PostConfigure<IOptions<DomainProjectionIdentityOptions>>(static (options, identity) =>
            {
                if (string.IsNullOrWhiteSpace(options.Workload))
                {
                    options.Workload = identity.Value.AppId;
                }
            });
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IHostedService, EventStoreDomainServiceSecurityStartupValidator>());
        return services;
    }

    /// <summary>
    /// Requires the catalog policy of a canonical operational route on an endpoint.
    /// </summary>
    /// <typeparam name="TBuilder">The endpoint convention builder type.</typeparam>
    /// <param name="builder">The endpoint convention builder.</param>
    /// <param name="route">The canonical route.</param>
    /// <returns>The builder.</returns>
    public static TBuilder RequireEventStoreDomainServicePolicy<TBuilder>(this TBuilder builder, string route)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        return EventStoreDomainServiceRoutes.TryGet(route, out EventStoreDomainServiceRoute? catalogRoute)
            ? builder.RequireAuthorization(catalogRoute!.Policy)
            : throw new ArgumentException("The route is not a canonical domain-service route.", nameof(route));
    }

    /// <summary>
    /// Requires an authenticated Dapr application channel and removes framework-supplied anonymous metadata, so a
    /// sidecar-originated route (such as the Dapr actor health route) cannot become an anonymous exception.
    /// </summary>
    /// <typeparam name="TBuilder">The endpoint convention builder type.</typeparam>
    /// <param name="builder">The endpoint convention builder.</param>
    /// <returns>The builder.</returns>
    public static TBuilder RequireEventStoreSidecarChannel<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Add(static endpointBuilder =>
        {
            for (int index = endpointBuilder.Metadata.Count - 1; index >= 0; index--)
            {
                if (endpointBuilder.Metadata[index] is IAllowAnonymous)
                {
                    endpointBuilder.Metadata.RemoveAt(index);
                }
            }
        });
        return builder.RequireAuthorization(EventStoreDomainServicePolicies.SidecarChannel);
    }

    /// <summary>
    /// Allows anonymous access to one literal GET or POST route. The startup inventory rejects a marker inherited
    /// by another route, a route template, and any canonical or sidecar-originated endpoint.
    /// </summary>
    /// <typeparam name="TBuilder">The endpoint convention builder type.</typeparam>
    /// <param name="builder">The endpoint convention builder.</param>
    /// <param name="route">The exact route pattern mapped by the host.</param>
    /// <returns>The builder.</returns>
    public static TBuilder AllowEventStorePublicEndpoint<TBuilder>(this TBuilder builder, string route)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(route);
        builder.Add(endpointBuilder => endpointBuilder.Metadata.Add(new EventStorePublicEndpointMetadata(route)));
        return builder.AllowAnonymous();
    }
}
