using System.Reflection;
using System.Runtime.CompilerServices;

using Hexalith.EventStore.Client.Configuration;
using Hexalith.EventStore.Client.Discovery;
using Hexalith.EventStore.Client.Registration;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Replay;
using Hexalith.EventStore.ServiceDefaults;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.EventStore.DomainService;

/// <summary>
/// One-line hosting extensions that let a domain module run on Hexalith.EventStore with only its domain
/// code plus a two-line host. The SDK provides every piece of infrastructure boilerplate a domain service
/// needs: Aspire service defaults (observability, health, resilience), convention-based discovery and
/// registration of aggregates/projections, runtime activation, and the canonical DAPR-invoked HTTP
/// endpoints (<c>/process</c>, <c>/replay-state</c>, <c>/admin/operational-index-metadata</c>).
/// </summary>
/// <remarks>
/// A conforming domain service is:
/// <code>
/// var builder = WebApplication.CreateBuilder(args);
/// builder.AddEventStoreDomainService();
/// var app = builder.Build();
/// app.UseEventStoreDomainService();
/// app.Run();
/// </code>
/// </remarks>
public static class EventStoreDomainServiceExtensions {
    /// <summary>
    /// Configures the host to run the EventStore domain types discovered in the <b>calling assembly</b>:
    /// wires Aspire service defaults and registers the discovered aggregates and projections.
    /// </summary>
    /// <param name="builder">The web application builder.</param>
    /// <returns>The builder for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is <c>null</c>.</exception>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static WebApplicationBuilder AddEventStoreDomainService(this WebApplicationBuilder builder)
        => AddEventStoreDomainServiceCore(builder, configureOptions: null, Assembly.GetCallingAssembly());

    /// <summary>
    /// Configures the host to run the EventStore domain types discovered in the <b>calling assembly</b>,
    /// applying the supplied global <see cref="EventStoreOptions"/>.
    /// </summary>
    /// <param name="builder">The web application builder.</param>
    /// <param name="configureOptions">A delegate to configure global EventStore options.</param>
    /// <returns>The builder for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> or <paramref name="configureOptions"/> is <c>null</c>.</exception>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static WebApplicationBuilder AddEventStoreDomainService(this WebApplicationBuilder builder, Action<EventStoreOptions> configureOptions) {
        ArgumentNullException.ThrowIfNull(configureOptions);
        return AddEventStoreDomainServiceCore(builder, configureOptions, Assembly.GetCallingAssembly());
    }

    /// <summary>
    /// Configures the host to run the EventStore domain types discovered in the <b>specified assemblies</b>.
    /// Use this overload when the domain logic lives in a separate library from the host (for example a
    /// <c>*.Server</c> project), passing a marker type's assembly such as
    /// <c>typeof(MyAggregate).Assembly</c>.
    /// </summary>
    /// <param name="builder">The web application builder.</param>
    /// <param name="domainAssemblies">The assemblies to scan for aggregate and projection types.</param>
    /// <returns>The builder for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> or <paramref name="domainAssemblies"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="domainAssemblies"/> is empty.</exception>
    public static WebApplicationBuilder AddEventStoreDomainService(this WebApplicationBuilder builder, params Assembly[] domainAssemblies)
        => AddEventStoreDomainServiceCore(builder, configureOptions: null, domainAssemblies);

    /// <summary>
    /// Configures the host to run the EventStore domain types discovered in the <b>specified assemblies</b>,
    /// applying the supplied global <see cref="EventStoreOptions"/>.
    /// </summary>
    /// <param name="builder">The web application builder.</param>
    /// <param name="configureOptions">A delegate to configure global EventStore options.</param>
    /// <param name="domainAssemblies">The assemblies to scan for aggregate and projection types.</param>
    /// <returns>The builder for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/>, <paramref name="configureOptions"/>, or <paramref name="domainAssemblies"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="domainAssemblies"/> is empty.</exception>
    public static WebApplicationBuilder AddEventStoreDomainService(this WebApplicationBuilder builder, Action<EventStoreOptions> configureOptions, params Assembly[] domainAssemblies) {
        ArgumentNullException.ThrowIfNull(configureOptions);
        return AddEventStoreDomainServiceCore(builder, configureOptions, domainAssemblies);
    }

    /// <summary>
    /// Activates the EventStore runtime and maps every endpoint a domain service exposes: the default
    /// health endpoints plus the canonical DAPR-invoked domain-service endpoints
    /// (see <see cref="MapEventStoreDomainService"/>).
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <returns>The application for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="app"/> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when <c>AddEventStoreDomainService()</c> was not called during service registration.</exception>
    public static WebApplication UseEventStoreDomainService(this WebApplication app) {
        ArgumentNullException.ThrowIfNull(app);

        // Populate the activation manifest (cascade-resolved DAPR resource names) from discovered domains.
        _ = app.UseEventStore();

        // Health endpoints (/health, /alive, /ready) from ServiceDefaults.
        _ = app.MapDefaultEndpoints();

        // Canonical domain-service endpoints invoked by the EventStore gateway.
        _ = app.MapEventStoreDomainService();

        return app;
    }

    /// <summary>
    /// Maps the canonical HTTP endpoints the EventStore gateway invokes on a domain service:
    /// <list type="bullet">
    /// <item><description><c>GET /</c> — a plain status root.</description></item>
    /// <item><description><c>POST /process</c> — routes a command to the keyed domain processor.</description></item>
    /// <item><description><c>POST /replay-state</c> — reconstructs aggregate state through the Apply convention.</description></item>
    /// <item><description><c>POST /query</c> — dispatches a query to the matching <see cref="IDomainQueryHandler"/>.</description></item>
    /// <item><description><c>POST /project</c> — dispatches a full-replay projection to the matching <see cref="IDomainProjectionHandler"/> (skipped when the app already mapped its own <c>/project</c>).</description></item>
    /// <item><description><c>POST /admin/operational-index-metadata</c> — returns the domain's command/event/projection catalog.</description></item>
    /// </list>
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <returns>The application for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="app"/> is <c>null</c>.</exception>
    public static WebApplication MapEventStoreDomainService(this WebApplication app) {
        ArgumentNullException.ThrowIfNull(app);

        _ = app.MapGet("/", () => "Hexalith EventStore domain service");

        _ = app.MapPost(
            "/process",
            async (DomainServiceRequest request, IServiceProvider serviceProvider)
                => Results.Ok(await DomainServiceRequestRouter.ProcessAsync(serviceProvider, request).ConfigureAwait(false)));

        _ = app.MapPost(
            "/replay-state",
            (AggregateReconstructionRequest request, IServiceProvider serviceProvider)
                => Results.Ok(DomainServiceRequestRouter.Replay(serviceProvider, request)));

        _ = app.MapPost(
            "/query",
            async (QueryEnvelope query, IServiceProvider serviceProvider)
                => Results.Ok(await DomainQueryDispatcher.ExecuteAsync(serviceProvider, query).ConfigureAwait(false)));

        // /project — the stateless full-replay projection endpoint (Model a). Dispatches to the matching
        // IDomainProjectionHandler. Skipped when the app already mapped its own /project so a domain with
        // bespoke projection wire behavior (e.g. a Tier-3 fault injector) takes precedence — registering the
        // SDK route on top of an existing one would make the request matcher ambiguous.
        if (!IsRouteMapped(app, "/project")) {
            _ = app.MapPost(
                "/project",
                (ProjectionRequest request, IServiceProvider serviceProvider) => {
                    ProjectionResponse? response = DomainProjectionDispatcher.Project(serviceProvider, request);
                    return response is null ? Results.NotFound() : Results.Ok(response);
                });
        }

        _ = app.MapPost(
            "/admin/operational-index-metadata",
            (AdminOperationalIndexMetadata.Request request, DiscoveryResult discovery, IEnumerable<IDomainQueryHandler> queryHandlers)
                => Results.Ok(AdminOperationalIndexMetadata.Create(discovery, request.Domains, queryHandlers)));

        return app;
    }

    private static WebApplicationBuilder AddEventStoreDomainServiceCore(
        WebApplicationBuilder builder,
        Action<EventStoreOptions>? configureOptions,
        params Assembly[] domainAssemblies) {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(domainAssemblies);
        if (domainAssemblies.Length == 0) {
            throw new ArgumentException("At least one domain assembly must be specified.", nameof(domainAssemblies));
        }

        // Observability, health checks, service discovery, and HTTP resilience.
        _ = builder.AddServiceDefaults();

        // Convention discovery + keyed IDomainProcessor registration for the domain assemblies.
        // The explicit-assemblies overload is used (never the calling-assembly one) so discovery targets
        // the domain — not this SDK assembly.
        _ = configureOptions is not null
            ? builder.Services.AddEventStore(configureOptions, domainAssemblies)
            : builder.Services.AddEventStore(domainAssemblies);

        // Discover and register IDomainQueryHandler implementations for the /query endpoint.
        AddDomainQueryHandlers(builder.Services, domainAssemblies);

        // Discover and register IDomainProjectionHandler implementations for the /project endpoint.
        AddDomainProjectionHandlers(builder.Services, domainAssemblies);

        return builder;
    }

    private static void AddDomainQueryHandlers(IServiceCollection services, Assembly[] domainAssemblies) {
        // Idempotent: skip if query handlers are already registered (e.g. a second call on the same services).
        if (services.Any(static s => s.ServiceType == typeof(IDomainQueryHandler))) {
            return;
        }

        foreach (Assembly assembly in domainAssemblies) {
            foreach (Type type in GetLoadableTypes(assembly)) {
                if (type is { IsClass: true, IsAbstract: false } && typeof(IDomainQueryHandler).IsAssignableFrom(type)) {
                    _ = services.AddScoped(typeof(IDomainQueryHandler), type);
                }
            }
        }
    }

    private static void AddDomainProjectionHandlers(IServiceCollection services, Assembly[] domainAssemblies) {
        // Idempotent: skip if projection handlers are already registered (e.g. a second call on the same services).
        if (services.Any(static s => s.ServiceType == typeof(IDomainProjectionHandler))) {
            return;
        }

        foreach (Assembly assembly in domainAssemblies) {
            foreach (Type type in GetLoadableTypes(assembly)) {
                if (type is { IsClass: true, IsAbstract: false } && typeof(IDomainProjectionHandler).IsAssignableFrom(type)) {
                    // Full-replay projection handlers are stateless (Model a) — registered as singletons so the
                    // /project endpoint can resolve them without a request scope.
                    _ = services.AddSingleton(typeof(IDomainProjectionHandler), type);
                }
            }
        }
    }

    private static bool IsRouteMapped(IEndpointRouteBuilder endpoints, string route) {
        foreach (EndpointDataSource dataSource in endpoints.DataSources) {
            foreach (Endpoint endpoint in dataSource.Endpoints) {
                if (endpoint is RouteEndpoint routeEndpoint
                    && string.Equals(routeEndpoint.RoutePattern.RawText, route, StringComparison.OrdinalIgnoreCase)) {
                    return true;
                }
            }
        }

        return false;
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly) {
        try {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex) {
            return ex.Types.OfType<Type>();
        }
    }
}
