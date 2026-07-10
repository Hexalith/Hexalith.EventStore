
using System.Reflection;
using System.Runtime.CompilerServices;

using Hexalith.EventStore.Client.Aggregates;
using Hexalith.EventStore.Client.Configuration;
using Hexalith.EventStore.Client.Discovery;
using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Client.Projections;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.Client.Registration;

/// <summary>
/// Extension methods for registering Event Store client services in the dependency injection container.
/// </summary>
public static class EventStoreServiceCollectionExtensions {
    /// <summary>
    /// Registers the typed HTTP EventStore gateway client.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">Optional client options.</param>
    /// <returns>The HTTP client builder for additional configuration.</returns>
    public static IHttpClientBuilder AddEventStoreGatewayClient(
        this IServiceCollection services,
        Action<EventStoreGatewayClientOptions>? configureOptions = null) {
        ArgumentNullException.ThrowIfNull(services);

        if (configureOptions is not null) {
            _ = services.Configure(configureOptions);
        }

        // Fail-closed command-status Location default (AD-17): generated command controllers always resolve
        // ICommandStatusLocationBuilder. Without an explicit gateway status base it emits no Location header;
        // opt into absolute mode with AddEventStoreCommandStatusLocation.
        services.TryAddSingleton<ICommandStatusLocationBuilder, CommandStatusLocationBuilder>();

        return services.AddHttpClient<IEventStoreGatewayClient, EventStoreGatewayClient>((serviceProvider, httpClient) => {
            EventStoreGatewayClientOptions options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<EventStoreGatewayClientOptions>>().Value;
            if (options.BaseAddress is not null) {
                httpClient.BaseAddress = options.BaseAddress;
            }
        });
    }

    /// <summary>
    /// Adds the platform-owned DAPR service-invocation routing handler to an HTTP client.
    /// </summary>
    /// <remarks>
    /// Architecture invariant AD-18 requires this extension to be called after authentication and
    /// forwarding handlers so it is the innermost delegating handler and has final ownership of the
    /// <c>dapr-app-id</c> and <c>dapr-api-token</c> headers.
    /// </remarks>
    /// <param name="builder">The HTTP client builder.</param>
    /// <param name="appId">The authoritative DAPR application id.</param>
    /// <param name="apiToken">The optional authoritative DAPR API token.</param>
    /// <returns>The HTTP client builder for additional configuration.</returns>
    public static IHttpClientBuilder AddEventStoreDaprServiceInvocation(
        this IHttpClientBuilder builder,
        string appId,
        string? apiToken = null) {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(appId);

        return builder.AddHttpMessageHandler(() => new DaprServiceInvocationHandler(appId, apiToken));
    }

    /// <summary>
    /// Configures the absolute browser-facing gateway origin used to emit the command-status <c>Location</c>
    /// header on generated command controllers' <c>202 Accepted</c> responses (architecture invariant AD-17).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="gatewayStatusBase">The absolute HTTP or HTTPS gateway origin clients poll for command status.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when the base is not an absolute HTTP/HTTPS origin URI.</exception>
    public static IServiceCollection AddEventStoreCommandStatusLocation(
        this IServiceCollection services,
        Uri gatewayStatusBase) {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(gatewayStatusBase);

        // Reject anything that is not an absolute http(s) origin. A query or fragment is rejected because the
        // builder appends the status path onto the base; a base like "https://gw/?x=1" would swallow the path
        // into the query and compose a dangling 404 Location. A path prefix is intentionally allowed (it
        // composes correctly for a gateway hosted behind a reverse-proxy path).
        if (!gatewayStatusBase.IsAbsoluteUri
            || (gatewayStatusBase.Scheme != Uri.UriSchemeHttp && gatewayStatusBase.Scheme != Uri.UriSchemeHttps)
            || !string.IsNullOrEmpty(gatewayStatusBase.UserInfo)
            || !string.IsNullOrEmpty(gatewayStatusBase.Query)
            || !string.IsNullOrEmpty(gatewayStatusBase.Fragment)) {
            throw new ArgumentException(
                "The gateway status base must be an absolute HTTP or HTTPS origin URI without user info, query, or fragment.",
                nameof(gatewayStatusBase));
        }

        _ = services.Configure<CommandStatusLocationOptions>(options => options.GatewayStatusBase = gatewayStatusBase);
        services.TryAddSingleton<ICommandStatusLocationBuilder, CommandStatusLocationBuilder>();
        return services;
    }

    /// <summary>
    /// Registers a domain processor implementation in the dependency injection container with scoped lifetime.
    /// </summary>
    /// <typeparam name="TProcessor">The domain processor implementation type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddEventStoreClient<TProcessor>(this IServiceCollection services)
        where TProcessor : class, IDomainProcessor {
        ArgumentNullException.ThrowIfNull(services);
        return services.AddScoped<IDomainProcessor, TProcessor>();
    }

    /// <summary>
    /// Auto-discovers aggregate and projection types in the calling assembly,
    /// registers discovered aggregates in the DI container, and stores full discovery results.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static IServiceCollection AddEventStore(this IServiceCollection services) {
        ArgumentNullException.ThrowIfNull(services);
        return AddEventStoreCore(services, configureOptions: null, [Assembly.GetCallingAssembly()]);
    }

    /// <summary>
    /// Auto-discovers aggregate and projection types in the calling assembly,
    /// registers discovered aggregates with the specified global options, and stores full discovery results.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">A delegate to configure <see cref="EventStoreOptions"/>.</param>
    /// <returns>The service collection for chaining.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static IServiceCollection AddEventStore(this IServiceCollection services, Action<EventStoreOptions> configureOptions) {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureOptions);
        return AddEventStoreCore(services, configureOptions, [Assembly.GetCallingAssembly()]);
    }

    /// <summary>
    /// Scans the specified assemblies for aggregate and projection types and registers them in the DI container.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="assemblies">The assemblies to scan for domain types.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddEventStore(this IServiceCollection services, params Assembly[] assemblies) {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(assemblies);
        if (assemblies.Length == 0) {
            throw new ArgumentException("At least one assembly must be specified.", nameof(assemblies));
        }

        return AddEventStoreCore(services, configureOptions: null, assemblies);
    }

    /// <summary>
    /// Scans the specified assemblies for aggregate and projection types and registers them with the specified global options.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">A delegate to configure <see cref="EventStoreOptions"/>.</param>
    /// <param name="assemblies">The assemblies to scan for domain types.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddEventStore(this IServiceCollection services, Action<EventStoreOptions> configureOptions, params Assembly[] assemblies) {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureOptions);
        ArgumentNullException.ThrowIfNull(assemblies);
        if (assemblies.Length == 0) {
            throw new ArgumentException("At least one assembly must be specified.", nameof(assemblies));
        }

        return AddEventStoreCore(services, configureOptions, assemblies);
    }

    private static IServiceCollection AddEventStoreCore(
        IServiceCollection services,
        Action<EventStoreOptions>? configureOptions,
        IEnumerable<Assembly> assemblies) {
        // Idempotency: skip if already registered
        if (services.Any(s => s.ServiceType == typeof(DiscoveryResult))) {
            return services;
        }

        // Configure options if provided
        if (configureOptions is not null) {
            _ = services.Configure(configureOptions);
        }

        // Opportunistic appsettings binding (AC3): bind EventStore section when IConfiguration is available.
        IConfiguration? configuration = TryGetConfiguration(services);
        if (configuration is not null) {
            _ = services.Configure<EventStoreOptions>(configuration.GetSection("EventStore"));
        }

        // Scan assemblies for domain types
        DiscoveryResult discoveryResult = AssemblyScanner.ScanForDomainTypes(assemblies);

        // Register DiscoveryResult as singleton
        _ = services.AddSingleton(discoveryResult);

        // Register empty activation context (populated by UseEventStore())
        _ = services.AddSingleton<EventStoreActivationContext>();

        // Register each discovered aggregate as IDomainProcessor
        foreach (DiscoveredDomain aggregate in discoveryResult.Aggregates) {
            // Non-keyed: backward compat + enumeration of all processors
            _ = services.AddScoped(typeof(IDomainProcessor), aggregate.Type);

            // Keyed: domain-specific resolution (forward-looking for actor pipeline)
            _ = services.AddKeyedScoped(typeof(IDomainProcessor), aggregate.DomainName, aggregate.Type);
        }

        // Register projections as themselves and initialize optional post-construction services.
        foreach (DiscoveredDomain projection in discoveryResult.Projections) {
            _ = services.AddScoped(projection.Type, serviceProvider => {
                object instance = ActivatorUtilities.CreateInstance(serviceProvider, projection.Type);

                if (instance is IEventStoreProjection eventStoreProjection) {
                    eventStoreProjection.Notifier = serviceProvider.GetService<IProjectionChangeNotifier>();
                    ILoggerFactory? loggerFactory = serviceProvider.GetService<ILoggerFactory>();
                    eventStoreProjection.Logger = loggerFactory?.CreateLogger(projection.Type);
                }

                return instance;
            });
        }

        return services;
    }

    private static IConfiguration? TryGetConfiguration(IServiceCollection services) {
        ArgumentNullException.ThrowIfNull(services);

        ServiceDescriptor? descriptor = services.LastOrDefault(static s => s.ServiceType == typeof(IConfiguration));
        if (descriptor?.ImplementationInstance is IConfiguration configurationInstance) {
            return configurationInstance;
        }

        if (descriptor is null) {
            return null;
        }

        using ServiceProvider tempProvider = services.BuildServiceProvider();
        return tempProvider.GetService<IConfiguration>();
    }
}
