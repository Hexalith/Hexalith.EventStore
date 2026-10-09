using System.Reflection;
using System.Runtime.CompilerServices;

using Hexalith.EventStore.Client.Attributes;
using Hexalith.EventStore.Client.Configuration;
using Hexalith.EventStore.Client.Discovery;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Client.Registration;
using Hexalith.EventStore.Client.Subscriptions;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Replay;
using Hexalith.EventStore.ServiceDefaults;

using Hexalith.EventStore.Contracts.Results;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
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
    /// <summary>Opts one domain into bounded V1 result serialization using an exact, declared profile.</summary>
    /// <remarks>The readable ceiling remains 1 MiB until a separately measured capability is admitted.</remarks>
    public static IServiceCollection AddEventStoreBoundedV1DomainSerialization(
        this IServiceCollection services,
        string domain,
        Action<BoundedV1DomainSerializerProfile> configure) {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        ArgumentNullException.ThrowIfNull(configure);
        if (services.Any(descriptor => descriptor.ServiceType == typeof(BoundedV1DomainResultProducer)
            && descriptor.IsKeyedService && string.Equals(descriptor.ServiceKey as string, domain, StringComparison.Ordinal))) {
            throw new ArgumentException("CapabilityMismatch: a bounded V1 serializer profile is already registered for this domain.", nameof(domain));
        }

        var profile = new BoundedV1DomainSerializerProfile();
        configure(profile);
        BoundedV1DomainResultProducer producer = profile.Build(1024 * 1024);
        services.AddKeyedSingleton<BoundedV1DomainResultProducer>(domain, (_, _) => producer);
        return services;
    }

    /// <summary>
    /// The DAPR topic-discovery route the sidecar reads to learn a service's subscriptions, stored without a
    /// leading slash because that is the form DAPR's own MapSubscribeHandler registers.
    /// </summary>
    private const string DaprSubscribeRoute = "dapr/subscribe";

    /// <summary>
    /// Marks CloudEvents unwrapping as already registered on this application, so a second activation call
    /// cannot add the middleware twice. Middleware exposes no route to inspect, unlike the mapped endpoints.
    /// </summary>
    private const string CloudEventsRegisteredKey = "Hexalith.EventStore.DomainService.CloudEventsRegistered";

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

        // DAPR pub/sub delivery for consumed domain events. Event-subscription plumbing is generic host
        // capability a domain module must not re-implement, so the canonical activation owns it instead of
        // leaving every module to rediscover the same gap: DAPR publishes application/cloudevents+json by
        // default, so a consumed envelope cannot bind without CloudEvents unwrapping, and the sidecar never
        // learns the topic exists without /dapr/subscribe. A module that registers a consumer and omits any
        // of the three has an unreachable consumer, an empty access projection, and — where that projection
        // gates authorization — every authorized read failing closed indefinitely.
        //
        // Wired only when a consumer is actually registered. EventStoreDomainEventProcessor is the singleton
        // the consumer registration adds, so a domain service that consumes nothing gains no routes and no
        // middleware, and its composed pipeline is unchanged.
        bool consumesDomainEvents = app.Services.GetService<EventStoreDomainEventProcessor>() is not null;

        // Unwrapping is middleware, so it must be registered before endpoint execution — hence here, ahead
        // of the mapping calls below rather than beside them.
        // WebApplication implements IApplicationBuilder explicitly, so the shared property bag — the only
        // place a middleware registration can leave a marker — is reached through the interface.
        IDictionary<string, object?> applicationProperties = ((IApplicationBuilder)app).Properties;

        if (consumesDomainEvents && !applicationProperties.ContainsKey(CloudEventsRegisteredKey)) {
            applicationProperties[CloudEventsRegisteredKey] = true;
            _ = app.UseCloudEvents();
        }

        // Health endpoints (/health, /alive, /ready) from ServiceDefaults.
        _ = app.MapDefaultEndpoints();

        // Canonical domain-service endpoints invoked by the EventStore gateway.
        _ = app.MapEventStoreDomainService();

        // Typed-reminder actor routes (AD-20 R6), wired only when AddEventStoreReminders registered the runtime.
        // MapEventStoreReminders skips the Dapr actor handlers when the host already mapped them; the
        // app-channel token filter is installed by the registration itself.
        if (app.Services.GetService<ReminderIntentIndex>() is not null) {
            _ = app.MapEventStoreReminders();
        }

        if (consumesDomainEvents) {
            EventStoreDomainEventsOptions domainEvents = app.Services
                .GetRequiredService<IOptions<EventStoreDomainEventsOptions>>()
                .Value;

            // Each mapping is skipped when the host already mapped it, so a host that wired its own
            // subscription surface stays authoritative — the same rule the DAPR client and Data Protection
            // registrations follow in AddEventStoreDomainServiceCore.
            if (!IsRouteMapped(app, domainEvents.SubscriptionRoute, HttpMethods.Post)) {
                _ = app.MapEventStoreDomainEvents();
            }

            if (!IsDaprSubscribeMapped(app)) {
                // Subscription discovery is a sidecar-originated call: it carries the app-channel token only.
                _ = app.MapSubscribeHandler().RequireEventStoreSidecarChannel();
            }
        }

        return app;
    }

    /// <summary>
    /// Maps the protected status root and the canonical HTTP endpoints the EventStore gateway invokes on a domain
    /// service. Every route requires the Dapr application-channel token plus a validated EventStore workload
    /// assertion. The status root uses <see cref="EventStoreDomainServicePolicies.AnyWorkload"/>; operational routes
    /// additionally require their catalog operation (see <see cref="EventStoreDomainServiceRoutes"/>):
    /// <list type="bullet">
    /// <item><description><c>GET /</c> — returns the constant <c>Hexalith EventStore domain service</c> label for any authenticated workload.</description></item>
    /// <item><description><c>POST /process</c> — routes a command to the keyed domain processor.</description></item>
    /// <item><description><c>POST /replay-state</c> — reconstructs aggregate state through the Apply convention.</description></item>
    /// <item><description><c>POST /query</c> — dispatches a query to the matching <see cref="IDomainQueryHandler"/>.</description></item>
    /// <item><description><c>POST /project</c> — dispatches a full-replay projection to the matching <see cref="IDomainProjectionHandler"/> (skipped when the app already mapped its own <c>/project</c>).</description></item>
    /// <item><description><c>POST /project/v2</c> — dispatches an admitted set to exact named async projection handlers.</description></item>
    /// <item><description><c>POST /project/rebuild/v1</c> — coordinates full-prefix named rebuild candidates as one durable batch.</description></item>
    /// <item><description><c>POST /project/rebuild/stage/v1</c> — stages named rebuild candidates without changing the visible view.</description></item>
    /// <item><description><c>POST /project/rebuild/commit/v1</c> — commits and reads back staged named rebuild candidates.</description></item>
    /// <item><description><c>POST /project/rebuild/abort/v1</c> — compensates an uncommitted named rebuild batch.</description></item>
    /// <item><description><c>POST /project/rebuild/verify/v1</c> — verifies staged or committed named rebuild evidence.</description></item>
    /// <item><description><c>POST /project/rebuild/shared/v1</c> — advances an additive tenant/domain shared rebuild session.</description></item>
    /// <item><description><c>POST /admin/operational-index-metadata</c> — returns the domain's command/event/projection catalog.</description></item>
    /// </list>
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <returns>The application for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="app"/> is <c>null</c>.</exception>
    public static WebApplication MapEventStoreDomainService(this WebApplication app) {
        ArgumentNullException.ThrowIfNull(app);

        ValidateDomainQueryHandlerRoutes(app.Services);
        app.Services.GetService<EventStoreDomainServiceEndpointSource>()?.Capture(app);
        bool mapProjectionEndpoint = !IsRouteMapped(app, "/project", HttpMethods.Post);
        bool mapNamedProjectionEndpoint = !IsRouteMapped(app, "/project/v2", HttpMethods.Post);
        if (mapProjectionEndpoint) {
            ValidateDomainProjectionHandlerRoutes(app.Services);
        }

        ValidateNamedDomainProjectionHandlerRoutes(app.Services);

        // Preserve the status-root contract without adding an anonymous endpoint (AD-16).
        _ = app.MapGet("/", () => "Hexalith EventStore domain service")
            .RequireAuthorization(EventStoreDomainServicePolicies.AnyWorkload);

        _ = app.MapPost(
            "/process",
            async (DomainServiceRequest request, HttpContext httpContext, IServiceProvider serviceProvider, CancellationToken cancellationToken) => {
                // Wire administrator flags are untrusted: rebuild them from the domain's current authorization. An
                // unavailable verifier is a bounded 503 before any domain work, never a raw server error.
                DomainServiceRequest verified;
                try {
                    verified = await DomainServiceAdministratorAssertions
                        .RebuildAsync(serviceProvider, request, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (DomainServiceAdministratorVerificationException) {
                    return DomainServiceAdministratorAssertions.VerifierUnavailable(httpContext);
                }

                DomainServiceWireResult result = await DomainServiceRequestRouter.ProcessAsync(serviceProvider, verified, cancellationToken).ConfigureAwait(false);
                return DomainServiceRequestRouter.HasBoundedV1Producer(serviceProvider, verified.Command.Domain)
                    ? (IResult)new BoundedV1WireResultResponse(result)
                    : Results.Ok(result);
            })
            .RequireEventStoreDomainServicePolicy("/process");

        _ = app.MapPost(
            "/replay-state",
            async (AggregateReconstructionRequest request, IServiceProvider serviceProvider, CancellationToken cancellationToken)
                => Results.Ok(await DomainServiceRequestRouter.ReplayAsync(serviceProvider, request, cancellationToken).ConfigureAwait(false)))
            .RequireEventStoreDomainServicePolicy("/replay-state");

        _ = app.MapPost(
            "/query",
            async (QueryEnvelope query, HttpContext httpContext, IServiceProvider serviceProvider, CancellationToken cancellationToken) => {
                QueryEnvelope verified;
                try {
                    verified = await DomainServiceAdministratorAssertions
                        .RebuildAsync(serviceProvider, query, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (DomainServiceAdministratorVerificationException) {
                    return DomainServiceAdministratorAssertions.VerifierUnavailable(httpContext);
                }

                return (IResult)Results.Ok(await DomainQueryDispatcher.ExecuteAsync(serviceProvider, verified, cancellationToken).ConfigureAwait(false));
            })
            .RequireEventStoreDomainServicePolicy("/query");

        // /project — the stateless full-replay projection endpoint (Model a). Dispatches to the matching
        // IDomainProjectionHandler. Skipped when the app already mapped its own /project so a domain with
        // bespoke projection wire behavior (e.g. a Tier-3 fault injector) takes precedence — registering the
        // SDK route on top of an existing one would make the request matcher ambiguous.
        if (mapProjectionEndpoint) {
            _ = app.MapPost(
                "/project",
                (ProjectionRequest request, IServiceProvider serviceProvider, CancellationToken cancellationToken) => {
                    cancellationToken.ThrowIfCancellationRequested();
                    ProjectionResponse? response = DomainProjectionDispatcher.Project(serviceProvider, request, cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    return response is null ? Results.NotFound() : Results.Ok(response);
                })
                .RequireEventStoreDomainServicePolicy("/project");
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
                })
                .RequireEventStoreDomainServicePolicy("/project/v2");

            _ = app.MapPost(
                "/project/v2/reconcile",
                async (ProjectionDispatchRequest request,
                       IServiceProvider serviceProvider,
                       IOptions<ProjectionDispatchOptions> projectionDispatchOptions,
                       IOptions<DomainProjectionIdentityOptions> projectionIdentityOptions,
                       CancellationToken cancellationToken) => {
                    try {
                        ProjectionDispatchResponse response = await DomainProjectionDispatcher
                            .ReconcileAsync(
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
                })
                .RequireEventStoreDomainServicePolicy("/project/v2/reconcile");
        }

        MapNamedProjectionRebuildEndpoint(app, "/project/rebuild/v1", DomainProjectionRebuildBatchAction.Execute);
        MapNamedProjectionRebuildEndpoint(app, "/project/rebuild/stage/v1", DomainProjectionRebuildBatchAction.Stage);
        MapNamedProjectionRebuildEndpoint(app, "/project/rebuild/commit/v1", DomainProjectionRebuildBatchAction.Commit);
        MapNamedProjectionRebuildEndpoint(app, "/project/rebuild/abort/v1", DomainProjectionRebuildBatchAction.Abort);
        MapNamedProjectionRebuildEndpoint(app, "/project/rebuild/verify/v1", DomainProjectionRebuildBatchAction.Verify);
        MapSharedProjectionRebuildEndpoint(app);

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
            })
            .RequireEventStoreDomainServicePolicy("/admin/operational-index-metadata");

        return app;
    }

    private static void MapNamedProjectionRebuildEndpoint(
        WebApplication app,
        string route,
        DomainProjectionRebuildBatchAction action) {
        if (IsRouteMapped(app, route, HttpMethods.Post)) {
            return;
        }

        _ = app.MapPost(
            route,
            async (ProjectionDispatchRequest request,
                   IServiceProvider serviceProvider,
                   IOptions<ProjectionDispatchOptions> projectionDispatchOptions,
                   IOptions<DomainProjectionIdentityOptions> projectionIdentityOptions,
                   CancellationToken cancellationToken) => {
                try {
                    ProjectionDispatchResponse response = action switch {
                        DomainProjectionRebuildBatchAction.Stage => await DomainProjectionDispatcher
                            .StageRebuildAsync(serviceProvider, request, projectionDispatchOptions.Value, projectionIdentityOptions.Value, cancellationToken)
                            .ConfigureAwait(false),
                        DomainProjectionRebuildBatchAction.Commit => await DomainProjectionDispatcher
                            .CommitRebuildAsync(serviceProvider, request, projectionDispatchOptions.Value, projectionIdentityOptions.Value, cancellationToken)
                            .ConfigureAwait(false),
                        DomainProjectionRebuildBatchAction.Abort => await DomainProjectionDispatcher
                            .AbortRebuildAsync(serviceProvider, request, projectionDispatchOptions.Value, projectionIdentityOptions.Value, cancellationToken)
                            .ConfigureAwait(false),
                        DomainProjectionRebuildBatchAction.Verify => await DomainProjectionDispatcher
                            .VerifyRebuildAsync(serviceProvider, request, projectionDispatchOptions.Value, projectionIdentityOptions.Value, cancellationToken)
                            .ConfigureAwait(false),
                        _ => await DomainProjectionDispatcher
                            .RebuildAsync(serviceProvider, request, projectionDispatchOptions.Value, projectionIdentityOptions.Value, cancellationToken)
                            .ConfigureAwait(false),
                    };
                    return (IResult)Results.Ok(response);
                }
                catch (ProjectionDispatchValidationException exception) {
                    return Results.BadRequest(exception.ReasonCode);
                }
            })
            .RequireEventStoreDomainServicePolicy(route);
    }

    private static void MapSharedProjectionRebuildEndpoint(WebApplication app) {
        const string route = "/project/rebuild/shared/v1";
        if (IsRouteMapped(app, route, HttpMethods.Post)) {
            return;
        }

        _ = app.MapPost(
            route,
            async (DomainSharedProjectionRebuildRequest request,
                   IServiceProvider serviceProvider,
                   IOptions<ProjectionDispatchOptions> projectionDispatchOptions,
                   IOptions<DomainProjectionIdentityOptions> projectionIdentityOptions,
                   CancellationToken cancellationToken) => {
                try {
                    DomainSharedProjectionRebuildResponse response = await DomainSharedProjectionRebuildDispatcher
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
            })
            .RequireEventStoreDomainServicePolicy(route);
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

        // Internal trust boundary (FR28): app-channel token + trusted-issuer workload assertion on every
        // operational route, sidecar-channel token on sidecar-originated deliveries, and an authenticated
        // fallback for every other endpoint. Only /health, /alive, and /ready stay anonymous.
        _ = builder.Services.AddEventStoreDomainServiceSecurity();

        // Domain services use DAPR-backed SDK facilities such as persisted read models. Keep this canonical
        // registration idempotent so a host-supplied client remains authoritative.
        if (!builder.Services.Any(static service => service.ServiceType == typeof(Dapr.Client.DaprClient))) {
            builder.Services.AddDaprClient();
        }

        // The query dispatch path constructs the platform-owned IQueryCursorCodec, whose cursor integrity is
        // Data Protection backed. That provider is generic host capability, so the canonical domain-service
        // registration owns it here rather than leaving every domain module to discover the gap and patch it
        // in its own composition root. Idempotent for the same reason as the DAPR client above: a host that
        // has already configured a key ring (persistence, protection at rest) stays authoritative.
        if (!builder.Services.Any(static service => service.ServiceType == typeof(IDataProtectionProvider))) {
            _ = builder.Services.AddDataProtection();
        }

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

    /// <summary>
    /// Detects an already-mapped DAPR topic-discovery endpoint, tolerating both spellings of the pattern.
    /// DAPR's own <c>MapSubscribeHandler</c> registers <c>dapr/subscribe</c> with no leading slash, while a
    /// host mapping its own handler typically writes <c>/dapr/subscribe</c> — verified against both, because
    /// an exact-text comparison silently misses one form and double-maps the route.
    /// </summary>
    private static bool IsDaprSubscribeMapped(IEndpointRouteBuilder endpoints) {
        foreach (EndpointDataSource dataSource in endpoints.DataSources) {
            foreach (Endpoint endpoint in dataSource.Endpoints) {
                if (endpoint is RouteEndpoint routeEndpoint
                    && string.Equals(
                        routeEndpoint.RoutePattern.RawText?.TrimStart('/'),
                        DaprSubscribeRoute,
                        StringComparison.OrdinalIgnoreCase)) {
                    return true;
                }
            }
        }

        return false;
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
