using System.Reflection;
using System.Runtime.CompilerServices;

using Hexalith.EventStore.Client.Attributes;
using Hexalith.EventStore.Client.Configuration;
using Hexalith.EventStore.Client.Discovery;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Client.Registration;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Replay;
using Hexalith.EventStore.ServiceDefaults;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.DomainService;

/// <summary>
/// One-line hosting extensions that let a domain module run on Hexalith.EventStore with only its domain
/// code plus a two-line host. The SDK provides every piece of infrastructure boilerplate a domain service
/// needs: Aspire service defaults (observability, health, resilience), convention-based discovery and
/// registration of aggregates/projections, runtime activation, and the canonical DAPR-invoked HTTP
/// endpoints (<c>/process</c>, <c>/replay-state</c>, <c>/query</c>, <c>/project</c>, and
/// <c>/project/v2</c>, and <c>/admin/operational-index-metadata</c>).
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
    /// <item><description><c>POST /project/v2</c> — dispatches an admitted set to exact named async projection handlers.</description></item>
    /// <item><description><c>POST /project/rebuild/v1</c> — coordinates full-prefix named rebuild candidates as one durable batch.</description></item>
    /// <item><description><c>POST /admin/operational-index-metadata</c> — returns the domain's command/event/projection catalog.</description></item>
    /// </list>
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <returns>The application for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="app"/> is <c>null</c>.</exception>
    public static WebApplication MapEventStoreDomainService(this WebApplication app) {
        ArgumentNullException.ThrowIfNull(app);

        ValidateDomainQueryHandlerRoutes(app.Services);
        bool mapProjectionEndpoint = !IsRouteMapped(app, "/project", HttpMethods.Post);
        bool mapNamedProjectionEndpoint = !IsRouteMapped(app, "/project/v2", HttpMethods.Post);
        bool mapNamedProjectionRebuildEndpoint = !IsRouteMapped(app, "/project/rebuild/v1", HttpMethods.Post);
        if (mapProjectionEndpoint) {
            ValidateDomainProjectionHandlerRoutes(app.Services);
        }

        ValidateNamedDomainProjectionHandlerRoutes(app.Services);

        _ = app.MapGet("/", () => "Hexalith EventStore domain service");

        _ = app.MapPost(
            "/process",
            async (DomainServiceRequest request, IServiceProvider serviceProvider, CancellationToken cancellationToken)
                => Results.Ok(await DomainServiceRequestRouter.ProcessAsync(serviceProvider, request, cancellationToken).ConfigureAwait(false)));

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
        if (mapProjectionEndpoint) {
            _ = app.MapPost(
                "/project",
                (ProjectionRequest request, IServiceProvider serviceProvider) => {
                    ProjectionResponse? response = DomainProjectionDispatcher.Project(serviceProvider, request);
                    return response is null ? Results.NotFound() : Results.Ok(response);
                });
        }

        if (mapNamedProjectionEndpoint) {
            _ = app.MapPost(
                "/project/v2",
                async (ProjectionDispatchRequest request,
                       IServiceProvider serviceProvider,
                       IOptions<ProjectionDispatchOptions> projectionDispatchOptions,
                       IOptions<DomainProjectionIdentityOptions> projectionIdentityOptions,
                       CancellationToken cancellationToken) => {
                    try {
                        ProjectionDispatchResponse response = await DomainProjectionDispatcher
                            .DispatchAsync(
                                serviceProvider,
                                request,
                                projectionDispatchOptions.Value,
                                projectionIdentityOptions.Value,
                                cancellationToken)
                            .ConfigureAwait(false);
                        return (IResult)Results.Ok(response);
                    }
                    catch (ProjectionDispatchValidationException exception) {
                        return Results.BadRequest(exception.ReasonCode);
                    }
                });
        }

        if (mapNamedProjectionRebuildEndpoint) {
            _ = app.MapPost(
                "/project/rebuild/v1",
                async (ProjectionDispatchRequest request,
                       IServiceProvider serviceProvider,
                       IOptions<ProjectionDispatchOptions> projectionDispatchOptions,
                       IOptions<DomainProjectionIdentityOptions> projectionIdentityOptions,
                       CancellationToken cancellationToken) => {
                    try {
                        ProjectionDispatchResponse response = await DomainProjectionDispatcher
                            .RebuildAsync(
                                serviceProvider,
                                request,
                                projectionDispatchOptions.Value,
                                projectionIdentityOptions.Value,
                                cancellationToken)
                            .ConfigureAwait(false);
                        return (IResult)Results.Ok(response);
                    }
                    catch (ProjectionDispatchValidationException exception) {
                        return Results.BadRequest(exception.ReasonCode);
                    }
                });
        }

        _ = app.MapPost(
            "/admin/operational-index-metadata",
            (AdminOperationalIndexMetadata.Request request,
             DiscoveryResult discovery,
             IEnumerable<IDomainQueryHandler> queryHandlers,
             IEnumerable<IAsyncDomainProjectionHandler> namedProjectionHandlers,
             IOptions<ProjectionDispatchOptions> projectionDispatchOptions,
             IOptions<DomainProjectionIdentityOptions> projectionIdentityOptions,
             [FromServices] DomainProjectionCatalogRegistry catalogRegistry) => {
                DomainProjectionIdentityOptions identity = projectionIdentityOptions.Value;
                AdminOperationalIndexMetadata.Response response;
                if (string.IsNullOrWhiteSpace(request.AppId) || string.IsNullOrWhiteSpace(request.ServiceVersion)) {
                    response = AdminOperationalIndexMetadata.Create(discovery, request.Domains, queryHandlers);
                }
                else {
                    if (!string.Equals(request.AppId, identity.AppId, StringComparison.Ordinal)
                        || !string.Equals(request.ServiceVersion, identity.ServiceVersion, StringComparison.Ordinal)
                        || request.Domains.Count != 1) {
                        return Results.BadRequest(ProjectionDispatchReasonCodes.UnsupportedCapability);
                    }

                    response = AdminOperationalIndexMetadata.Create(
                        discovery,
                        request.Domains,
                        queryHandlers,
                        namedProjectionHandlers,
                        identity.AppId,
                        identity.ServiceVersion,
                        projectionDispatchOptions.Value);
                }

                RegisterNamedProjectionCatalog(response, catalogRegistry);
                return Results.Ok(response);
            });

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
        _ = builder.Services.AddSingleton<DomainProjectionCatalogRegistry>();
        _ = builder.Services.AddOptions<ProjectionDispatchOptions>()
            .BindConfiguration("EventStore:ProjectionDispatch");
        _ = builder.Services.AddOptions<DomainProjectionIdentityOptions>()
            .BindConfiguration("EventStore:DomainService")
            .PostConfigure(options => {
                options.AppId = string.IsNullOrWhiteSpace(options.AppId)
                    ? Environment.GetEnvironmentVariable("DAPR_APP_ID") ?? builder.Environment.ApplicationName
                    : options.AppId;
                options.ServiceVersion = string.IsNullOrWhiteSpace(options.ServiceVersion)
                    ? "v1"
                    : options.ServiceVersion;
            })
            .Validate(options => !string.IsNullOrWhiteSpace(options.AppId)
                && !string.IsNullOrWhiteSpace(options.ServiceVersion));

        DiscoveryResult discovery = GetRegisteredDiscoveryResult(builder.Services);
        _ = builder.Services.AddEventStoreDomainTelemetry(GetDiscoveredDomainNames(discovery, domainAssemblies));

        return builder;
    }

    private static DiscoveryResult GetRegisteredDiscoveryResult(IServiceCollection services)
        => services.LastOrDefault(static descriptor => descriptor.ServiceType == typeof(DiscoveryResult))?.ImplementationInstance as DiscoveryResult
            ?? throw new InvalidOperationException("AddEventStore did not register domain discovery results.");

    private static IEnumerable<string> GetDiscoveredDomainNames(DiscoveryResult discovery, Assembly[] domainAssemblies)
        => discovery.Aggregates
            .Concat(discovery.Projections)
            .Select(static domain => domain.DomainName)
            .Concat(GetHandlerDomainNames<IDomainQueryHandler>(domainAssemblies))
            .Concat(GetHandlerDomainNames<IDomainProjectionHandler>(domainAssemblies))
            .Concat(GetHandlerDomainNames<IAsyncDomainProjectionHandler>(domainAssemblies))
            .Distinct(StringComparer.OrdinalIgnoreCase);

    private static IEnumerable<string> GetHandlerDomainNames<THandler>(Assembly[] domainAssemblies) {
        foreach (Assembly assembly in domainAssemblies) {
            foreach (Type type in assembly.GetTypes()) {
                if (type is not { IsClass: true, IsAbstract: false } || !typeof(THandler).IsAssignableFrom(type)) {
                    continue;
                }

                EventStoreDomainAttribute? attribute = type.GetCustomAttribute<EventStoreDomainAttribute>();
                if (attribute is not null) {
                    yield return attribute.DomainName;
                    continue;
                }

                if (type.GetConstructor(Type.EmptyTypes) is null) {
                    continue;
                }

                if (Activator.CreateInstance(type) is THandler handler) {
                    yield return handler switch {
                        IDomainQueryHandler queryHandler => queryHandler.Domain,
                        IDomainProjectionHandler projectionHandler => projectionHandler.Domain,
                        IAsyncDomainProjectionHandler projectionHandler => projectionHandler.Domain,
                        _ => throw new InvalidOperationException($"Unsupported domain handler type '{typeof(THandler).FullName}'."),
                    };
                }
            }
        }
    }

    private static void AddDomainQueryHandlers(IServiceCollection services, Assembly[] domainAssemblies) {
        // Idempotent: skip if query handlers are already registered (e.g. a second call on the same services).
        if (services.Any(static s => s.ServiceType == typeof(IDomainQueryHandler))) {
            return;
        }

        foreach (Assembly assembly in domainAssemblies) {
            foreach (Type type in assembly.GetTypes()) {
                if (type is { IsClass: true, IsAbstract: false } && typeof(IDomainQueryHandler).IsAssignableFrom(type)) {
                    _ = services.AddScoped(typeof(IDomainQueryHandler), type);
                }
            }
        }
    }

    private static void AddDomainProjectionHandlers(IServiceCollection services, Assembly[] domainAssemblies) {
        bool registerLegacyHandlers = !services.Any(static service => service.ServiceType == typeof(IDomainProjectionHandler));
        bool registerAsyncHandlers = !services.Any(static service => service.ServiceType == typeof(IAsyncDomainProjectionHandler));

        foreach (Assembly assembly in domainAssemblies) {
            foreach (Type type in assembly.GetTypes()) {
                if (registerLegacyHandlers
                    && type is { IsClass: true, IsAbstract: false }
                    && typeof(IDomainProjectionHandler).IsAssignableFrom(type)) {
                    // Full-replay projection handlers are stateless (Model a) — registered as singletons so the
                    // /project endpoint can resolve them without a request scope.
                    _ = services.AddSingleton(typeof(IDomainProjectionHandler), type);
                }

                if (registerAsyncHandlers
                    && type is { IsClass: true, IsAbstract: false }
                    && typeof(IAsyncDomainProjectionHandler).IsAssignableFrom(type)) {
                    // Named projection handlers own persistence resources and therefore resolve per request scope.
                    _ = services.AddScoped(typeof(IAsyncDomainProjectionHandler), type);
                }

                RegisterDeclaredProjectionReadModelSlots(services, type);
            }
        }
    }

    // A domain declares its aggregate-owned vs shared read-model slots by implementing the static
    // IDeclaresProjectionReadModelSlots contract (a Client-package type — no raw DAPR plumbing, AD-2).
    // Each declaration is registered as a DI singleton; the platform slot registry absorbs them all when
    // it is resolved. The static declaration is read without instantiating the type.
    private static void RegisterDeclaredProjectionReadModelSlots(IServiceCollection services, Type type) {
        if (type is not { IsClass: true, IsAbstract: false }
            || !typeof(IDeclaresProjectionReadModelSlots).IsAssignableFrom(type)) {
            return;
        }

        PropertyInfo? property = type.GetProperty(
            nameof(IDeclaresProjectionReadModelSlots.ProjectionReadModelSlots),
            BindingFlags.Public | BindingFlags.Static);
        if (property?.GetValue(null) is not IReadOnlyList<ProjectionReadModelSlotDeclaration> slots) {
            return;
        }

        foreach (ProjectionReadModelSlotDeclaration slot in slots) {
            _ = services.AddSingleton(slot);
        }
    }

    private static bool IsRouteMapped(IEndpointRouteBuilder endpoints, string route, string httpMethod) {
        foreach (EndpointDataSource dataSource in endpoints.DataSources) {
            foreach (Endpoint endpoint in dataSource.Endpoints) {
                if (endpoint is RouteEndpoint routeEndpoint
                    && string.Equals(routeEndpoint.RoutePattern.RawText, route, StringComparison.OrdinalIgnoreCase)
                    && MatchesHttpMethod(routeEndpoint, httpMethod)) {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool MatchesHttpMethod(RouteEndpoint endpoint, string httpMethod) {
        IHttpMethodMetadata? metadata = endpoint.Metadata.GetMetadata<IHttpMethodMetadata>();
        return metadata is null
            || metadata.HttpMethods.Contains(httpMethod, StringComparer.OrdinalIgnoreCase);
    }

    private static void ValidateDomainQueryHandlerRoutes(IServiceProvider serviceProvider) {
        using IServiceScope scope = serviceProvider.CreateScope();
        _ = DomainQueryHandlerRouteValidator.MaterializeAndValidate(scope.ServiceProvider.GetServices<IDomainQueryHandler>());
    }

    private static void ValidateDomainProjectionHandlerRoutes(IServiceProvider serviceProvider) {
        using IServiceScope scope = serviceProvider.CreateScope();
        _ = DomainProjectionHandlerRouteValidator.MaterializeAndValidate(scope.ServiceProvider.GetServices<IDomainProjectionHandler>());
    }

    private static void ValidateNamedDomainProjectionHandlerRoutes(IServiceProvider serviceProvider) {
        using IServiceScope scope = serviceProvider.CreateScope();
        ProjectionDispatchOptions options = scope.ServiceProvider
            .GetService<IOptions<ProjectionDispatchOptions>>()
            ?.Value
            ?? new ProjectionDispatchOptions();
        _ = DomainProjectionHandlerRouteValidator.MaterializeAndValidateNamed(
            scope.ServiceProvider.GetServices<IAsyncDomainProjectionHandler>(),
            options);
    }

    private static void RegisterNamedProjectionCatalog(
        AdminOperationalIndexMetadata.Response response,
        DomainProjectionCatalogRegistry catalogRegistry) {
        if (string.IsNullOrWhiteSpace(response.CatalogFingerprint)) {
            return;
        }

        catalogRegistry.Register(
            response.CatalogFingerprint,
            response.Domains
                .Where(static domain => domain.NamedProjectionTypes is { Count: > 0 })
                .SelectMany(domain => domain.NamedProjectionTypes!
                    .Select(projectionType => new ProjectionDispatchRoute(domain.Domain, projectionType))));
    }
}
