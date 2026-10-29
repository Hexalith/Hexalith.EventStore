using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text;
using System.Text.Json;

using Dapr.Client;

using Hexalith.Commons.UniqueIds;
using Hexalith.EventStore.Client.Discovery;
using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Client.Registration;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.DomainService.Tests.Fixtures;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.DomainService.Tests;

/// <summary>
/// Tier 1 coverage of the domain-service SDK host extensions and the moved router/metadata helpers.
/// </summary>
public sealed class EventStoreDomainServiceExtensionsTests {
    /// <summary>Selective mapping preserves bespoke routes and protects every SDK projection endpoint.</summary>
    [Fact]
    public void SelectiveProjectionMappingPreservesHostRoutesAndWorkloadPolicies()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        _ = builder.AddEventStoreDomainService();
        WebApplication app = builder.Build();
        foreach (string route in new[] { "/process", "/query", "/replay-state", "/project" })
            _ = app.MapPost(route, () => Results.Ok());

        _ = app.MapEventStoreDomainProjectionEndpoints();

        RouteEndpoint[] endpoints = GetRouteEndpoints(app);
        foreach (string route in new[] { "/process", "/query", "/replay-state", "/project" })
            endpoints.Count(endpoint => endpoint.RoutePattern.RawText == route).ShouldBe(1);
        endpoints.ShouldNotContain(endpoint => endpoint.RoutePattern.RawText == "/");
        endpoints.ShouldNotContain(endpoint => endpoint.RoutePattern.RawText == "/admin/operational-index-metadata");
        foreach (RouteEndpoint endpoint in endpoints.Where(endpoint => endpoint.RoutePattern.RawText!.StartsWith("/project/", StringComparison.Ordinal)))
        {
            string route = endpoint.RoutePattern.RawText!;
            EventStoreDomainServiceRoute catalog = EventStoreDomainServiceRoutes.Operational.Single(item => item.Route == route);
            endpoint.Metadata.GetOrderedMetadata<Microsoft.AspNetCore.Authorization.IAuthorizeData>()
                .Select(data => data.Policy).ShouldBe([catalog.Policy]);
        }
        AssertRouteSupports(app, "/project/v2", HttpMethods.Post);
        AssertRouteSupports(app, "/project/rebuild/v1", HttpMethods.Post);
        AssertRouteSupports(app, "/project/rebuild/part/v1", HttpMethods.Post);
        AssertRouteSupports(app, "/project/rebuild/shared/v1", HttpMethods.Post);

        _ = app.MapEventStoreDomainProjectionMetadataEndpoint();
        RouteEndpoint metadata = GetRouteEndpoints(app).Single(endpoint => endpoint.RoutePattern.RawText == "/admin/operational-index-metadata");
        metadata.Metadata.GetOrderedMetadata<Microsoft.AspNetCore.Authorization.IAuthorizeData>()
            .Select(data => data.Policy).ShouldBe([EventStoreDomainServiceRoutes.Operational.Single(item => item.Route == "/admin/operational-index-metadata").Policy]);
    }

    [Fact]
    public void ProjectionOnlyMappingsPreserveCustomRoutesAndCanonicalPolicies()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.AddEventStoreDomainService(typeof(WidgetAggregate).Assembly);
        WebApplication app = builder.Build();
        app.MapPost("/project", () => Results.Ok("owner-projection"));
        app.MapPost("/query", () => Results.Ok("owner-query"));
        app.MapEventStoreDomainProjectionEndpoints();
        app.MapEventStoreDomainProjectionMetadataEndpoint();
        app.MapEventStoreDomainProjectionEndpoints();
        app.MapEventStoreDomainProjectionMetadataEndpoint();
        RouteEndpoint[] endpoints = GetRouteEndpoints(app);
        endpoints.Count(endpoint => endpoint.RoutePattern.RawText == "/project").ShouldBe(1);
        endpoints.Count(endpoint => endpoint.RoutePattern.RawText == "/query").ShouldBe(1);
        endpoints.ShouldNotContain(endpoint => endpoint.RoutePattern.RawText == "/process");
        foreach (var route in EventStoreDomainServiceRoutes.Operational.Where(route =>
            route.Route.StartsWith("/project", StringComparison.Ordinal)
            || route.Route == "/admin/operational-index-metadata"))
        {
            if (route.Route == "/project") { continue; }
            RouteEndpoint endpoint = endpoints.Single(endpoint => endpoint.RoutePattern.RawText == route.Route);
            endpoint.Metadata.GetOrderedMetadata<Microsoft.AspNetCore.Authorization.IAuthorizeData>()
                .ShouldContain(policy => policy.Policy == route.Policy);
        }
    }

    [Fact]
    public void MetadataReportsRegisteredHandlerOnlyDomainWithoutGrantingOrphanProjectionCapabilities()
    {
        IDomainQueryHandler handler = Substitute.For<IDomainQueryHandler>();
        handler.Domain.Returns("folders"); handler.QueryType.Returns("OwnerRead");
        IAsyncDomainProjectionHandler orphan = Substitute.For<IAsyncDomainProjectionHandler>();
        orphan.Domain.Returns("folders"); orphan.ProjectionType.Returns("orphan");
        var discovery = new DiscoveryResult([], []);
        var metadata = AdminOperationalIndexMetadata.Create(discovery, ["folders"], [handler], [orphan], "folders", "v1", new());
        var domain = metadata.Domains.ShouldHaveSingleItem();
        domain.QueryTypes.ShouldBe(["OwnerRead"]); domain.CommandTypes.ShouldBeEmpty(); domain.EventTypes.ShouldBeEmpty();
        domain.NamedProjectionTypes.ShouldBeNull(); domain.OrdinaryProjectionTypes.ShouldBeNull(); metadata.DispatchCapability.ShouldBeNull();
        AdminOperationalIndexMetadata.Create(discovery, ["other"], [handler]).Domains.ShouldBeEmpty();
        AdminOperationalIndexMetadata.Create(discovery, null, [handler]).Domains.ShouldHaveSingleItem().Domain.ShouldBe("folders");
    }

    [Fact]
    public void AddEventStoreDomainService_RegistersDurableSharedScratchSweep()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();

        _ = builder.AddEventStoreDomainService();

        builder.Services.ShouldContain(descriptor => descriptor.ServiceType == typeof(IHostedService)
            && descriptor.ImplementationType == typeof(DomainSharedProjectionScratchCleanupService));
    }

    /// <summary>Proves the canonical domain-service registration supplies the shared DAPR client.</summary>
    [Fact]
    public void AddEventStoreDomainService_RegistersDaprClient() {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();

        _ = builder.AddEventStoreDomainService();

        ServiceDescriptor descriptor = builder.Services
            .Single(service => service.ServiceType == typeof(DaprClient));
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
    }

    /// <summary>Proves an existing DAPR registration is retained without adding another client.</summary>
    [Fact]
    public void AddEventStoreDomainService_PreservesExistingDaprClientRegistration() {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Services.AddDaprClient();
        int before = builder.Services.Count(service => service.ServiceType == typeof(DaprClient));

        _ = builder.AddEventStoreDomainService();

        builder.Services.Count(service => service.ServiceType == typeof(DaprClient)).ShouldBe(before);
    }

    /// <summary>
    /// Proves the no-argument overload discovers domains in the <b>calling</b> assembly (this test
    /// assembly), not the SDK assembly — the critical <c>GetCallingAssembly</c> capture contract.
    /// </summary>
    [Fact]
    public void AddEventStoreDomainService_NoArguments_DiscoversCallingAssemblyDomains() {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();

        _ = builder.AddEventStoreDomainService();

        using ServiceProvider provider = builder.Services.BuildServiceProvider();
        DiscoveryResult discovery = provider.GetRequiredService<DiscoveryResult>();
        discovery.Aggregates.ShouldContain(a => a.DomainName == "widget");
    }

    /// <summary>
    /// Proves the explicit-assemblies overload (used when domain logic lives in a separate library,
    /// e.g. a <c>*.Server</c> project) scans exactly the supplied assembly.
    /// </summary>
    [Fact]
    public void AddEventStoreDomainService_ExplicitAssembly_DiscoversSuppliedAssemblyDomains() {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();

        _ = builder.AddEventStoreDomainService(typeof(WidgetAggregate).Assembly);

        using ServiceProvider provider = builder.Services.BuildServiceProvider();
        DiscoveryResult discovery = provider.GetRequiredService<DiscoveryResult>();
        discovery.Aggregates.ShouldContain(a => a.DomainName == "widget");
    }

    /// <summary>
    /// Proves the canonical host registers convention-named diagnostics for each distinct discovered domain.
    /// </summary>
    [Fact]
    public void AddEventStoreDomainService_RegistersDiagnosticsForDiscoveredDomains() {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();

        _ = builder.AddEventStoreDomainService();

        using ServiceProvider provider = builder.Services.BuildServiceProvider();
        EventStoreDomainDiagnosticsRegistry registry = provider.GetRequiredService<EventStoreDomainDiagnosticsRegistry>();

        registry.Domains.OrderBy(static domain => domain, StringComparer.Ordinal).ShouldBe(["catalog", "gadget", "widget"]);
        registry.GetDiagnostics("widget")!.ActivitySource.Name.ShouldBe(EventStoreDomainTelemetry.ActivitySourceName("widget"));
        registry.GetDiagnostics("gadget")!.Meter.Name.ShouldBe(EventStoreDomainTelemetry.MeterName("gadget"));
        registry.GetDiagnostics("catalog")!.Domain.ShouldBe("catalog");
    }

    /// <summary>
    /// Proves the canonical two-line SDK host activates discovered domains and maps every default and
    /// domain-service endpoint exposed by a conforming domain module.
    /// </summary>
    [Fact]
    public void UseEventStoreDomainService_ActivatesDiscoveredDomainsAndMapsCanonicalEndpoints() {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        _ = builder.AddEventStoreDomainService();
        WebApplication app = builder.Build();

        _ = app.UseEventStoreDomainService();

        EventStoreActivationContext activation = app.Services.GetRequiredService<EventStoreActivationContext>();
        activation.Activations.ShouldContain(a => a.DomainName == "widget");

        AssertRouteSupports(app, "/", HttpMethods.Get);
        RouteEndpoint rootEndpoint = GetRouteEndpoints(app).Single(endpoint => endpoint.RoutePattern.RawText == "/");
        rootEndpoint.Metadata.GetMetadata<Microsoft.AspNetCore.Authorization.IAllowAnonymous>().ShouldBeNull();
        rootEndpoint.Metadata.GetOrderedMetadata<Microsoft.AspNetCore.Authorization.IAuthorizeData>()
            .Select(static data => data.Policy)
            .ShouldBe([EventStoreDomainServicePolicies.AnyWorkload]);
        AssertRouteSupports(app, "/health", HttpMethods.Get);
        AssertRouteSupports(app, "/alive", HttpMethods.Get);
        AssertRouteSupports(app, "/ready", HttpMethods.Get);
        AssertRouteSupports(app, "/process", HttpMethods.Post);
        AssertRouteSupports(app, "/replay-state", HttpMethods.Post);
        AssertRouteSupports(app, "/query", HttpMethods.Post);
        AssertRouteSupports(app, "/project", HttpMethods.Post);
        AssertRouteSupports(app, "/admin/operational-index-metadata", HttpMethods.Post);
    }

    /// <summary>
    /// Proves the SDK owns DAPR event-subscription plumbing for a domain that consumes domain events, so a
    /// domain module never has to re-implement it. Without the subscription route the registered consumer is
    /// unreachable, and without <c>/dapr/subscribe</c> the sidecar never learns the topic exists — either way
    /// the module's projection stays empty and any authorization it gates fails closed indefinitely.
    /// </summary>
    [Fact]
    public void UseEventStoreDomainService_ConsumingDomain_MapsSubscriptionSurface() {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        _ = builder.AddEventStoreDomainService();
        _ = builder.Services.AddEventStoreDomainEvents(
            typeof(EventStoreDomainServiceExtensionsTests).Assembly,
            options => options.SubscriptionRoute = "/widget/events");
        WebApplication app = builder.Build();

        _ = app.UseEventStoreDomainService();

        AssertRouteSupports(app, "/widget/events", HttpMethods.Post);

        // DAPR's own MapSubscribeHandler registers the pattern WITHOUT a leading slash, so match on the
        // normalized form rather than the pretty one.
        GetRouteEndpoints(app)
            .Any(static endpoint => string.Equals(
                endpoint.RoutePattern.RawText?.TrimStart('/'),
                "dapr/subscribe",
                StringComparison.OrdinalIgnoreCase))
            .ShouldBeTrue("the sidecar cannot discover the topic without a subscribe handler");
    }

    /// <summary>
    /// Proves the subscription surface is wired only for domains that actually consume events. A domain
    /// service registering no consumer must gain no routes and no middleware from this activation.
    /// </summary>
    [Fact]
    public void UseEventStoreDomainService_NonConsumingDomain_MapsNoSubscriptionSurface() {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        _ = builder.AddEventStoreDomainService();
        WebApplication app = builder.Build();

        _ = app.UseEventStoreDomainService();

        GetRouteEndpoints(app)
            .Any(static endpoint => string.Equals(
                endpoint.RoutePattern.RawText?.TrimStart('/'),
                "dapr/subscribe",
                StringComparison.OrdinalIgnoreCase))
            .ShouldBeFalse("a domain service that consumes no events must not gain a subscription surface");
    }

    /// <summary>
    /// Proves the host stays authoritative: a host that already mapped its own subscription surface keeps
    /// exactly one endpoint per route, mirroring the DAPR client and Data Protection registration rule.
    /// </summary>
    [Fact]
    public void UseEventStoreDomainService_PreMappedSubscriptionSurface_IsNotDuplicated() {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        _ = builder.AddEventStoreDomainService();
        _ = builder.Services.AddEventStoreDomainEvents(
            typeof(EventStoreDomainServiceExtensionsTests).Assembly,
            options => options.SubscriptionRoute = "/widget/events");
        WebApplication app = builder.Build();

        _ = app.MapPost("/widget/events", () => "bespoke subscription handler");
        _ = app.MapSubscribeHandler();

        _ = app.UseEventStoreDomainService();

        GetRouteEndpoints(app)
            .Count(static endpoint => string.Equals(
                endpoint.RoutePattern.RawText,
                "/widget/events",
                StringComparison.OrdinalIgnoreCase))
            .ShouldBe(1);
        GetRouteEndpoints(app)
            .Count(static endpoint => string.Equals(
                endpoint.RoutePattern.RawText?.TrimStart('/'),
                "dapr/subscribe",
                StringComparison.OrdinalIgnoreCase))
            .ShouldBe(1);
    }

    /// <summary>
    /// Proves the SDK yields when an app maps its own exact <c>/project</c> endpoint before the canonical
    /// endpoint set, preserving bespoke projection wire behavior without creating ambiguous route matches.
    /// </summary>
    [Fact]
    public void UseEventStoreDomainService_PreservesPreMappedProjectRouteWithoutDuplicate() {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        _ = builder.AddEventStoreDomainService();
        WebApplication app = builder.Build();

        _ = app.MapPost("/project", () => "bespoke projection handler");

        _ = app.UseEventStoreDomainService();

        RouteEndpoint[] projectEndpoints = GetRouteEndpoints(app)
            .Where(static endpoint => string.Equals(endpoint.RoutePattern.RawText, "/project", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        projectEndpoints.Length.ShouldBe(1);
        EndpointSupportsHttpMethod(projectEndpoints[0], HttpMethods.Post).ShouldBeTrue();
    }

    /// <summary>
    /// Proves projection-handler validation is tied to the SDK-owned <c>/project</c> route. If an app maps
    /// its own POST <c>/project</c>, the SDK yields and does not fail the host for handlers it will not route.
    /// </summary>
    [Fact]
    public void UseEventStoreDomainService_PreMappedProjectRouteSkipsProjectionHandlerValidation() {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        _ = builder.AddEventStoreDomainService();
        IDomainProjectionHandler duplicateProjection = CreateDuplicateWidgetProjectionHandler();
        _ = builder.Services.AddSingleton(duplicateProjection);
        WebApplication app = builder.Build();

        _ = app.MapPost("/project", () => "bespoke projection handler");

        Should.NotThrow(() => app.UseEventStoreDomainService());

        RouteEndpoint[] projectEndpoints = GetRouteEndpoints(app)
            .Where(static endpoint => string.Equals(endpoint.RoutePattern.RawText, "/project", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        projectEndpoints.Length.ShouldBe(1);
        EndpointSupportsHttpMethod(projectEndpoints[0], HttpMethods.Post).ShouldBeTrue();
    }

    /// <summary>
    /// Proves a non-POST <c>/project</c> route does not suppress the canonical projection endpoint needed by
    /// DAPR service invocation.
    /// </summary>
    [Fact]
    public void UseEventStoreDomainService_MapsProjectPostWhenOnlyNonPostProjectRouteExists() {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        _ = builder.AddEventStoreDomainService();
        WebApplication app = builder.Build();

        _ = app.MapGet("/project", () => "diagnostic view");

        _ = app.UseEventStoreDomainService();

        RouteEndpoint[] projectEndpoints = GetRouteEndpoints(app)
            .Where(static endpoint => string.Equals(endpoint.RoutePattern.RawText, "/project", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        projectEndpoints.Length.ShouldBe(2);
        projectEndpoints.Any(endpoint => EndpointSupportsHttpMethod(endpoint, HttpMethods.Get)).ShouldBeTrue();
        projectEndpoints.Any(endpoint => EndpointSupportsHttpMethod(endpoint, HttpMethods.Post)).ShouldBeTrue();
    }

    /// <summary>
    /// Proves the <c>/process</c> routing path: the request router resolves the keyed processor for the
    /// command's domain and returns the produced events.
    /// </summary>
    [Fact]
    public async Task DomainServiceRequestRouter_Process_DispatchesToKeyedProcessor() {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        _ = builder.AddEventStoreDomainService();
        using ServiceProvider provider = builder.Services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();

        DomainServiceRequest request = new(
            new CommandEnvelope(
                MessageId: UniqueIdHelper.GenerateSortableUniqueStringId(),
                TenantId: "test-tenant",
                Domain: "widget",
                AggregateId: "widget-1",
                CommandType: nameof(CreateWidget),
                Payload: JsonSerializer.SerializeToUtf8Bytes(new CreateWidget()),
                CorrelationId: "corr-1",
                CausationId: null,
                UserId: "test-user",
                Extensions: null),
            CurrentState: null);

        DomainServiceWireResult result = await DomainServiceRequestRouter.ProcessAsync(scope.ServiceProvider, request);

        result.IsRejection.ShouldBeFalse();
        DomainServiceWireEvent @event = result.Events.ShouldHaveSingleItem();
        @event.EventTypeName.ShouldBe(typeof(WidgetCreated).FullName);
    }

    /// <summary>
    /// Proves the no-hook path keeps the existing direct processor dispatch behavior without requiring any
    /// admission-chain registration.
    /// </summary>
    [Fact]
    public async Task DomainServiceRequestRouter_Process_WithoutAdmissionStages_DispatchesDirectlyToProcessor() {
        var calls = new List<string>();
        var processor = new RecordingWidgetProcessor(calls);
        ServiceProvider provider = BuildAdmissionTestProvider(processor);

        DomainServiceWireResult result = await DomainServiceRequestRouter.ProcessAsync(provider, CreateProcessRequest());

        result.IsRejection.ShouldBeFalse();
        result.Events.ShouldHaveSingleItem().EventTypeName.ShouldBe(typeof(WidgetCreated).FullName);
        processor.InvocationCount.ShouldBe(1);
        calls.ShouldBe(["processor"]);
    }

    /// <summary>
    /// Proves an accepting admission stage runs before the keyed processor and lets the processor result flow
    /// through unchanged.
    /// </summary>
    [Fact]
    public async Task DomainServiceRequestRouter_Process_AcceptingAdmissionStage_RunsBeforeProcessor() {
        var calls = new List<string>();
        var processor = new RecordingWidgetProcessor(calls);
        ServiceProvider provider = BuildAdmissionTestProvider(
            processor,
            services => services.AddEventStoreDomainAdmissionStage(
                _ => new RecordingAdmissionStage("auth", calls)));

        DomainServiceWireResult result = await DomainServiceRequestRouter.ProcessAsync(provider, CreateProcessRequest());

        result.IsRejection.ShouldBeFalse();
        result.Events.ShouldHaveSingleItem().EventTypeName.ShouldBe(typeof(WidgetCreated).FullName);
        processor.InvocationCount.ShouldBe(1);
        calls.ShouldBe(["auth", "processor"]);
    }

    /// <summary>
    /// Proves the builder-level generic registration API wires scoped admission stages in registration order.
    /// </summary>
    [Fact]
    public async Task AddEventStoreDomainAdmissionStage_BuilderGenericOverload_RegistersStagesInOrder() {
        var calls = new List<string>();
        var processor = new RecordingWidgetProcessor(calls);
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        _ = builder.Services.AddSingleton<IList<string>>(calls);
        _ = builder.Services.AddKeyedSingleton<IDomainProcessor>("widget", processor);

        _ = builder.AddEventStoreDomainAdmissionStage<FirstRegisteredAdmissionStage>();
        _ = builder.AddEventStoreDomainAdmissionStage<SecondRegisteredAdmissionStage>();

        using ServiceProvider provider = builder.Services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();

        DomainServiceWireResult result = await DomainServiceRequestRouter.ProcessAsync(scope.ServiceProvider, CreateProcessRequest());

        result.IsRejection.ShouldBeFalse();
        processor.InvocationCount.ShouldBe(1);
        calls.ShouldBe(["first", "second", "processor"]);
    }

    /// <summary>
    /// Proves an admission rejection is returned as a typed domain rejection and the keyed processor is never
    /// invoked after the rejection.
    /// </summary>
    [Fact]
    public async Task DomainServiceRequestRouter_Process_RejectingAdmissionStage_ReturnsTypedRejectionWithoutProcessor() {
        var calls = new List<string>();
        var processor = new RecordingWidgetProcessor(calls);
        ServiceProvider provider = BuildAdmissionTestProvider(
            processor,
            services => services.AddEventStoreDomainAdmissionStage(
                _ => new RecordingAdmissionStage("approval-gate", calls, accept: false)));

        DomainServiceWireResult result = await DomainServiceRequestRouter.ProcessAsync(provider, CreateProcessRequest());

        result.IsRejection.ShouldBeTrue();
        DomainServiceWireEvent @event = result.Events.ShouldHaveSingleItem();
        @event.EventTypeName.ShouldBe(typeof(WidgetRejected).FullName);
        @event.SerializationFormat.ShouldBe("json");
        JsonSerializer.Deserialize<WidgetRejected>(@event.Payload)!.Reason.ShouldBe("blocked");
        processor.InvocationCount.ShouldBe(0);
        calls.ShouldBe(["approval-gate"]);
    }

    /// <summary>
    /// Proves multiple admission stages execute in DI registration order and stop after the first rejection.
    /// </summary>
    [Fact]
    public async Task DomainServiceRequestRouter_Process_MultipleAdmissionStages_StopAfterFirstRejection() {
        var calls = new List<string>();
        var processor = new RecordingWidgetProcessor(calls);
        ServiceProvider provider = BuildAdmissionTestProvider(
            processor,
            services => {
                _ = services.AddEventStoreDomainAdmissionStage(_ => new RecordingAdmissionStage("auth", calls));
                _ = services.AddEventStoreDomainAdmissionStage(_ => new RecordingAdmissionStage("approval-gate", calls, accept: false));
                _ = services.AddEventStoreDomainAdmissionStage(_ => new RecordingAdmissionStage("pre-commit-audit", calls));
            });

        DomainServiceWireResult result = await DomainServiceRequestRouter.ProcessAsync(provider, CreateProcessRequest());

        result.IsRejection.ShouldBeTrue();
        processor.InvocationCount.ShouldBe(0);
        calls.ShouldBe(["auth", "approval-gate"]);
    }

    /// <summary>
    /// Proves a canceled request stops before admission or processor dispatch.
    /// </summary>
    [Fact]
    public async Task DomainServiceRequestRouter_Process_CanceledAdmissionStage_PropagatesCancellationWithoutProcessor() {
        var calls = new List<string>();
        var processor = new RecordingWidgetProcessor(calls);
        ServiceProvider provider = BuildAdmissionTestProvider(
            processor,
            services => services.AddEventStoreDomainAdmissionStage(
                _ => new CancellationAwareAdmissionStage("auth", calls)));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        _ = await Should.ThrowAsync<OperationCanceledException>(
            () => DomainServiceRequestRouter.ProcessAsync(provider, CreateProcessRequest(), cancellation.Token));

        processor.InvocationCount.ShouldBe(0);
        calls.ShouldBeEmpty();
    }

    /// <summary>
    /// Proves admission telemetry remains optional and uses the convention-named domain diagnostics when present.
    /// </summary>
    [Fact]
    public async Task DomainServiceRequestRouter_Process_AdmissionTelemetry_UsesDomainDiagnosticsWhenRegistered() {
        var calls = new List<string>();
        var processor = new RecordingWidgetProcessor(calls);
        using var listener = new ActivityListener();
        var activities = new List<Activity>();
        listener.ShouldListenTo = source => source.Name == EventStoreDomainTelemetry.ActivitySourceName("widget");
        listener.Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded;
        listener.ActivityStopped = activity => activities.Add(activity);
        ActivitySource.AddActivityListener(listener);

        ServiceProvider provider = BuildAdmissionTestProvider(
            processor,
            services => {
                _ = services.AddSingleton(new EventStoreDomainDiagnostics("widget"));
                _ = services.AddEventStoreDomainAdmissionStage(_ => new RecordingAdmissionStage("auth", calls));
            });

        DomainServiceWireResult result = await DomainServiceRequestRouter.ProcessAsync(provider, CreateProcessRequest());

        result.IsRejection.ShouldBeFalse();
        Activity activity = activities.ShouldHaveSingleItem();
        activity.OperationName.ShouldBe("eventstore.domain.admission.stage");
        activity.GetTagItem("eventstore.domain").ShouldBe("widget");
        activity.GetTagItem("eventstore.command.type").ShouldBe(nameof(CreateWidget));
        activity.GetTagItem("eventstore.admission.stage").ShouldBe("auth");
        activity.GetTagItem("eventstore.admission.accepted").ShouldBe(true);
    }

    /// <summary>
    /// Proves multi-domain admission telemetry resolves diagnostics from the command domain.
    /// </summary>
    [Fact]
    public async Task DomainServiceRequestRouter_Process_AdmissionTelemetry_UsesRequestDomainDiagnostics() {
        var calls = new List<string>();
        using var listener = new ActivityListener();
        var activities = new List<Activity>();
        listener.ShouldListenTo = source => source.Name.StartsWith(EventStoreDomainTelemetry.Prefix, StringComparison.Ordinal);
        listener.Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded;
        listener.ActivityStopped = activity => activities.Add(activity);
        ActivitySource.AddActivityListener(listener);

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        _ = builder.AddEventStoreDomainService();
        _ = builder.Services.AddSingleton<IList<string>>(calls);
        _ = builder.Services.AddEventStoreDomainAdmissionStage(
            serviceProvider => new RecordingAdmissionStage("auth", serviceProvider.GetRequiredService<IList<string>>()));
        using ServiceProvider provider = builder.Services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();

        DomainServiceWireResult result = await DomainServiceRequestRouter.ProcessAsync(scope.ServiceProvider, CreateGadgetProcessRequest());

        result.IsRejection.ShouldBeFalse();
        result.Events.ShouldHaveSingleItem().EventTypeName.ShouldBe(typeof(GadgetCreated).FullName);
        Activity activity = activities.ShouldHaveSingleItem();
        activity.Source.Name.ShouldBe(EventStoreDomainTelemetry.ActivitySourceName("gadget"));
        activity.GetTagItem("eventstore.domain").ShouldBe("gadget");
        activity.GetTagItem("eventstore.command.type").ShouldBe(nameof(CreateGadget));
    }

    /// <summary>
    /// Proves the admission histogram records a measurement tagged with the request domain, command
    /// type, and acceptance result — locking the metric contract, not just the emitted span.
    /// </summary>
    [Fact]
    public async Task DomainServiceRequestRouter_Process_AdmissionMetric_RecordsRequestDomainMeasurement() {
        var measurements = new List<(string? Domain, string? CommandType, bool? Accepted)>();
        using var meterListener = new MeterListener();
        meterListener.InstrumentPublished = (instrument, listener) => {
            if (instrument.Meter.Name == EventStoreDomainTelemetry.MeterName("gadget")
                && instrument.Name == "eventstore.domain.admission.stage.duration") {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        meterListener.SetMeasurementEventCallback<double>((_, _, tags, _) => {
            string? domain = null;
            string? commandType = null;
            bool? accepted = null;
            foreach (KeyValuePair<string, object?> tag in tags) {
                switch (tag.Key) {
                    case "eventstore.domain": domain = tag.Value as string; break;
                    case "eventstore.command.type": commandType = tag.Value as string; break;
                    case "eventstore.admission.accepted": accepted = tag.Value as bool?; break;
                    default: break;
                }
            }

            measurements.Add((domain, commandType, accepted));
        });
        meterListener.Start();

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        _ = builder.AddEventStoreDomainService();
        var calls = new List<string>();
        _ = builder.Services.AddSingleton<IList<string>>(calls);
        _ = builder.Services.AddEventStoreDomainAdmissionStage(
            serviceProvider => new RecordingAdmissionStage("auth", serviceProvider.GetRequiredService<IList<string>>()));
        using ServiceProvider provider = builder.Services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();

        _ = await DomainServiceRequestRouter.ProcessAsync(scope.ServiceProvider, CreateGadgetProcessRequest());

        (string? Domain, string? CommandType, bool? Accepted) measurement = measurements.ShouldHaveSingleItem();
        measurement.Domain.ShouldBe("gadget");
        measurement.CommandType.ShouldBe(nameof(CreateGadget));
        measurement.Accepted.ShouldBe(true);
    }

    /// <summary>
    /// Proves the operational-index metadata helper surfaces the domain's command and event types.
    /// </summary>
    [Fact]
    public void AdminOperationalIndexMetadata_Create_IncludesCommandAndEventTypes() {
        DiscoveryResult discovery = new(
            [new DiscoveredDomain(typeof(WidgetAggregate), "widget", typeof(WidgetState), DomainKind.Aggregate)],
            []);

        AdminOperationalIndexMetadata.Response response = AdminOperationalIndexMetadata.Create(discovery, ["widget"]);

        AdminOperationalIndexMetadata.DomainMetadata widget = response.Domains.ShouldHaveSingleItem();
        widget.Domain.ShouldBe("widget");
        widget.CommandTypes.ShouldContain(typeof(CreateWidget).FullName!);
        widget.EventTypes.ShouldContain(typeof(WidgetCreated).FullName!);
    }

    /// <summary>
    /// Proves the operational-index metadata reports the domain's handler-served query types (A7b-1), so the
    /// gateway can later route those query types to the domain's <c>/query</c> endpoint.
    /// </summary>
    [Fact]
    public void AdminOperationalIndexMetadata_Create_IncludesHandlerQueryTypes() {
        DiscoveryResult discovery = new(
            [new DiscoveredDomain(typeof(WidgetAggregate), "widget", typeof(WidgetState), DomainKind.Aggregate)],
            []);

        AdminOperationalIndexMetadata.Response response =
            AdminOperationalIndexMetadata.Create(discovery, ["widget"], [new WidgetQueryHandler()]);

        AdminOperationalIndexMetadata.DomainMetadata widget = response.Domains.ShouldHaveSingleItem();
        widget.QueryTypes.ShouldContain("get-widget");
    }

    /// <summary>
    /// Proves the released metadata response constructor, deconstructor, and legacy JSON shape remain intact.
    /// </summary>
    [Fact]
    public void AdminOperationalIndexMetadata_Response_PreservesLegacyAbiAndJsonShape() {
        var response = new AdminOperationalIndexMetadata.Response([]);
        response.Deconstruct(out IReadOnlyList<AdminOperationalIndexMetadata.DomainMetadata> domains);

        string json = JsonSerializer.Serialize(response, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        domains.ShouldBeEmpty();
        json.ShouldBe("{\"domains\":[]}");
    }

    /// <summary>
    /// Proves named routes are exact, ordinally sorted, capability bound, and fingerprinted deterministically.
    /// </summary>
    [Fact]
    public void AdminOperationalIndexMetadata_Create_IncludesDeterministicNamedDispatchCatalog() {
        DiscoveryResult discovery = new(
            [new DiscoveredDomain(typeof(WidgetAggregate), "widget", typeof(WidgetState), DomainKind.Aggregate)],
            []);
        IAsyncDomainProjectionHandler detail = CreateNamedProjectionHandler("widget", "widget-detail");
        IAsyncDomainProjectionHandler index = CreateNamedProjectionHandler("widget", "widget-index");

        AdminOperationalIndexMetadata.Response first = AdminOperationalIndexMetadata.Create(
            discovery,
            ["widget"],
            queryHandlers: null,
            namedProjectionHandlers: [index, detail],
            appId: "widget-service",
            serviceVersion: "1.2.0",
            options: new ProjectionDispatchOptions());
        AdminOperationalIndexMetadata.Response second = AdminOperationalIndexMetadata.Create(
            discovery,
            ["widget"],
            queryHandlers: null,
            namedProjectionHandlers: [detail, index],
            appId: "widget-service",
            serviceVersion: "1.2.0",
            options: new ProjectionDispatchOptions());

        AdminOperationalIndexMetadata.DomainMetadata widget = first.Domains.ShouldHaveSingleItem();
        widget.NamedProjectionTypes.ShouldBe(["widget-detail", "widget-index"]);
        first.DispatchVersion.ShouldBe(ProjectionDispatchProtocol.Version);
        first.DispatchCapability.ShouldBe(ProjectionDispatchProtocol.Capability);
        first.AppId.ShouldBe("widget-service");
        first.ServiceVersion.ShouldBe("1.2.0");
        first.CatalogFingerprint.ShouldNotBeNullOrWhiteSpace();
        first.CatalogFingerprint.ShouldBe(second.CatalogFingerprint);
    }

    [Fact]
    public void AdminOperationalIndexMetadata_AdvertisesOnlyFullReplayRoutesWithinCompleteCatalog() {
        DiscoveryResult discovery = new(
            [new DiscoveredDomain(typeof(WidgetAggregate), "widget", typeof(WidgetState), DomainKind.Aggregate)],
            []);
        IAsyncDomainProjectionRebuildHandler detail = Substitute.For<IAsyncDomainProjectionRebuildHandler>();
        detail.OrdinaryDeliveryEnabled.Returns(true);
        detail.Domain.Returns("widget");
        detail.ProjectionType.Returns("widget-detail");
        detail.RebuildSemantics.Returns(DomainProjectionRebuildSemantics.FullReplay);
        IAsyncDomainProjectionHandler shared = CreateNamedProjectionHandler("widget", "widget-index");

        AdminOperationalIndexMetadata.Response metadata = AdminOperationalIndexMetadata.Create(
            discovery, ["widget"], queryHandlers: null, namedProjectionHandlers: [shared, detail],
            appId: "widget-service", serviceVersion: "v1", options: new ProjectionDispatchOptions());

        AdminOperationalIndexMetadata.DomainMetadata domain = metadata.Domains.ShouldHaveSingleItem();
        domain.NamedProjectionTypes.ShouldBe(["widget-detail", "widget-index"]);
        domain.FullReplayProjectionTypes.ShouldBe(["widget-detail"]);
        metadata.CatalogFingerprint.ShouldBe(ProjectionRouteCatalogFingerprint.Compute("widget-service", "v1",
            [new ProjectionDispatchRoute("widget", "widget-detail"),
                new ProjectionDispatchRoute("widget", "widget-index")]));
    }

    [Fact]
    public void AdminOperationalIndexMetadata_AdvertisesStreamedOnlyWhenLegacyRouteIsAbsent() {
        DiscoveryResult discovery = new([], []);
        IAsyncDomainProjectionRebuildHandler streamed = Substitute.For<
            IAsyncDomainProjectionRebuildHandler, IAsyncDomainProjectionStreamedRebuildHandler>();
        streamed.Domain.Returns("timesheets");
        streamed.ProjectionType.Returns("time-entry-evidence");
        streamed.RebuildSemantics.Returns(DomainProjectionRebuildSemantics.FullReplay);

        AdminOperationalIndexMetadata.Response mapped = AdminOperationalIndexMetadata.Create(
            discovery, ["timesheets"], null, [streamed], "timesheets", "v1",
            new ProjectionDispatchOptions(), ["timesheets"], []);
        mapped.Domains.ShouldHaveSingleItem().MappedOnlyStreamedProjectionTypes
            .ShouldBe(["time-entry-evidence"]);

        IDomainProjectionHandler legacy = Substitute.For<IDomainProjectionHandler>();
        legacy.Domain.Returns("timesheets");
        AdminOperationalIndexMetadata.Response mixed = AdminOperationalIndexMetadata.Create(
            discovery, ["timesheets"], null, [streamed], "timesheets", "v1",
            new ProjectionDispatchOptions(), ["timesheets"], [legacy]);
        mixed.Domains.ShouldHaveSingleItem().MappedOnlyStreamedProjectionTypes.ShouldBeEmpty();
    }

    [Fact]
    public void AdminOperationalIndexMetadata_AdvertisesSingleEventHandlerCapability() {
        DiscoveryResult discovery = new([], []);
        IAsyncDomainProjectionSingleEventHandler single = Substitute.For<IAsyncDomainProjectionSingleEventHandler>();
        single.Domain.Returns("timesheets");
        single.ProjectionType.Returns("magic-link-token-hash-capability-index");

        AdminOperationalIndexMetadata.Response metadata = AdminOperationalIndexMetadata.Create(
            discovery, ["timesheets"], null, [single], "timesheets", "v1",
            new ProjectionDispatchOptions(), ["timesheets"], []);

        metadata.Domains.ShouldHaveSingleItem().SingleEventProjectionTypes
            .ShouldBe(["magic-link-token-hash-capability-index"]);
    }

    /// <summary>Proves app and service bindings participate in the route-catalog fingerprint.</summary>
    [Fact]
    public void AdminOperationalIndexMetadata_Create_BindsFingerprintToAppAndServiceVersion() {
        DiscoveryResult discovery = new(
            [new DiscoveredDomain(typeof(WidgetAggregate), "widget", typeof(WidgetState), DomainKind.Aggregate)],
            []);
        IAsyncDomainProjectionHandler handler = CreateNamedProjectionHandler("widget", "widget-detail");

        AdminOperationalIndexMetadata.Response first = AdminOperationalIndexMetadata.Create(
            discovery,
            ["widget"],
            null,
            [handler],
            "widget-service",
            "1.2.0",
            new ProjectionDispatchOptions());
        AdminOperationalIndexMetadata.Response stale = AdminOperationalIndexMetadata.Create(
            discovery,
            ["widget"],
            null,
            [handler],
            "widget-service",
            "1.1.0",
            new ProjectionDispatchOptions());
        AdminOperationalIndexMetadata.Response otherApp = AdminOperationalIndexMetadata.Create(
            discovery,
            ["widget"],
            null,
            [handler],
            "other-widget-service",
            "1.2.0",
            new ProjectionDispatchOptions());

        first.CatalogFingerprint.ShouldNotBe(stale.CatalogFingerprint);
        first.CatalogFingerprint.ShouldNotBe(otherApp.CatalogFingerprint);
    }

    [Fact]
    public void AdminOperationalIndexMetadata_Create_ExcludesOrphanNamedRouteFromFingerprint() {
        DiscoveryResult discovery = new(
            [new DiscoveredDomain(typeof(WidgetAggregate), "widget", typeof(WidgetState), DomainKind.Aggregate)],
            []);
        IAsyncDomainProjectionHandler valid = CreateNamedProjectionHandler("widget", "widget-detail");
        IAsyncDomainProjectionHandler orphan = CreateNamedProjectionHandler("orphan", "orphan-detail");

        AdminOperationalIndexMetadata.Response response = AdminOperationalIndexMetadata.Create(
            discovery,
            ["widget", "orphan"],
            null,
            [orphan, valid],
            "widget-service",
            "v1",
            new ProjectionDispatchOptions());

        response.Domains.ShouldHaveSingleItem().Domain.ShouldBe("widget");
        response.CatalogFingerprint.ShouldBe(ProjectionRouteCatalogFingerprint.Compute(
            "widget-service",
            "v1",
            [new ProjectionDispatchRoute("widget", "widget-detail")]));
    }

    [Fact]
    public void AdminOperationalIndexMetadata_Create_IncludesNamedRouteForKeyedProcessorDomain() {
        DiscoveryResult discovery = new([], []);
        IAsyncDomainProjectionHandler handler = CreateNamedProjectionHandler("timesheets", "tenant-activity-type-catalog");
        IDomainQueryHandler query = Substitute.For<IDomainQueryHandler>();
        query.Domain.Returns("timesheets");
        query.QueryType.Returns("get-timesheet");

        AdminOperationalIndexMetadata.Response response = AdminOperationalIndexMetadata.Create(
            discovery,
            ["timesheets"],
            [query],
            [handler],
            "timesheets",
            "v1",
            new ProjectionDispatchOptions(),
            keyedProcessorDomains: ["timesheets"]);

        AdminOperationalIndexMetadata.DomainMetadata domain = response.Domains.ShouldHaveSingleItem();
        domain.Domain.ShouldBe("timesheets");
        domain.QueryTypes.ShouldBe(["get-timesheet"]);
        domain.NamedProjectionTypes.ShouldBe(["tenant-activity-type-catalog"]);
        response.CatalogFingerprint.ShouldBe(ProjectionRouteCatalogFingerprint.Compute(
            "timesheets", "v1", [new ProjectionDispatchRoute("timesheets", "tenant-activity-type-catalog")]));
    }

    [Fact]
    public async Task NamedProjectionMetadata_IncludesAsyncOnlyKeyedProcessorDomain() {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Configuration["EventStore:DomainService:AppId"] = "timesheets";
        builder.Configuration["EventStore:DomainService:ServiceVersion"] = "v1";
        _ = builder.AddEventStoreDomainService();
        _ = builder.Services.AddKeyedScoped<IAsyncDomainProcessor>(
            "async-only",
            static (_, _) => Substitute.For<IAsyncDomainProcessor>());
        _ = builder.Services.AddScoped(_ => CreateNamedProjectionHandler("async-only", "async-only-index"));
        WebApplication app = builder.Build();
        _ = app.UseEventStoreDomainService();

        var request = new AdminOperationalIndexMetadata.Request(["async-only"]) {
            AppId = "timesheets",
            ServiceVersion = "v1",
        };
        (int status, string body) = await InvokeEndpointAsync(app, "/admin/operational-index-metadata", request);

        status.ShouldBe(StatusCodes.Status200OK, body);
        body.ShouldContain("async-only-index");
        body.ShouldContain("catalogFingerprint");
    }

    /// <summary>Legacy metadata recognizes both server-registered processor forms without granting v2 capabilities.</summary>
    /// <param name="asynchronous">Whether the domain uses an asynchronous keyed processor.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LegacyMetadataIncludesOnlyRegisteredKeyedDomainsAndPreservesNamedCatalog(bool asynchronous) {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Configuration["EventStore:DomainService:AppId"] = "sample";
        builder.Configuration["EventStore:DomainService:ServiceVersion"] = "v1";
        _ = builder.AddEventStoreDomainService(typeof(WidgetAggregate).Assembly);
        if (asynchronous)
            _ = builder.Services.AddKeyedScoped<IAsyncDomainProcessor>("keyed-only", static (_, _) => Substitute.For<IAsyncDomainProcessor>());
        else
            _ = builder.Services.AddKeyedScoped<IDomainProcessor>("keyed-only", static (_, _) => Substitute.For<IDomainProcessor>());
        _ = builder.Services.AddScoped(_ => CreateNamedProjectionHandler("keyed-only", "keyed-index"));
        _ = builder.Services.AddScoped(_ => CreateNamedProjectionHandler("orphan", "orphan-index"));
        await using WebApplication app = builder.Build();
        _ = app.UseEventStoreDomainService();
        var namedRequest = new AdminOperationalIndexMetadata.Request(["keyed-only"]) { AppId = "sample", ServiceVersion = "v1" };
        (int namedStatus, string namedBefore) = await InvokeEndpointAsync(app, "/admin/operational-index-metadata", namedRequest);
        namedStatus.ShouldBe(StatusCodes.Status200OK, namedBefore);

        (int status, string body) = await InvokeEndpointAsync(app, "/admin/operational-index-metadata",
            new AdminOperationalIndexMetadata.Request(["keyed-only", "widget", "orphan", "unknown"]));
        status.ShouldBe(StatusCodes.Status200OK, body);
        AdminOperationalIndexMetadata.Response legacy = JsonSerializer.Deserialize<AdminOperationalIndexMetadata.Response>(body,
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        legacy.Domains.Select(domain => domain.Domain).ShouldBe(["keyed-only", "widget"]);
        AdminOperationalIndexMetadata.DomainMetadata keyed = legacy.Domains.Single(domain => domain.Domain == "keyed-only");
        keyed.AggregateTypes.ShouldBeEmpty(); keyed.CommandTypes.ShouldBeEmpty(); keyed.EventTypes.ShouldBeEmpty();
        keyed.NamedProjectionTypes.ShouldBeNull(); keyed.OrdinaryProjectionTypes.ShouldBeNull();
        legacy.Domains.Single(domain => domain.Domain == "widget").AggregateTypes.ShouldNotBeEmpty();
        legacy.DispatchVersion.ShouldBeNull(); legacy.DispatchCapability.ShouldBeNull(); legacy.CatalogFingerprint.ShouldBeNull();
        body.ShouldNotContain("dispatchVersion"); body.ShouldNotContain("catalogFingerprint");

        (int repeatedStatus, string namedAfter) = await InvokeEndpointAsync(app, "/admin/operational-index-metadata", namedRequest);
        repeatedStatus.ShouldBe(StatusCodes.Status200OK, namedAfter);
        namedAfter.ShouldBe(namedBefore);
        AdminOperationalIndexMetadata.Response named = JsonSerializer.Deserialize<AdminOperationalIndexMetadata.Response>(namedAfter,
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        named.CatalogFingerprint.ShouldBe(ProjectionRouteCatalogFingerprint.Compute("sample", "v1",
            [new ProjectionDispatchRoute("keyed-only", "keyed-index")]));
        named.DispatchCapability.ShouldBe(ProjectionDispatchProtocol.Capability);
        named.Domains.ShouldHaveSingleItem().NamedProjectionTypes.ShouldBe(["keyed-index"]);
    }

    /// <summary>Legacy casing aliases retain one query-bearing domain without changing named dispatch metadata.</summary>
    [Fact]
    public void LegacyMetadataKeyedDomainCasingRetainsQueryMetadataWithoutChangingNamedCatalog()
    {
        DiscoveryResult discovery = new([], []);
        IDomainQueryHandler query = Substitute.For<IDomainQueryHandler>();
        query.Domain.Returns("sample"); query.QueryType.Returns("get-sample");
        IAsyncDomainProjectionHandler projection = CreateNamedProjectionHandler("sample", "sample-index");
        var namedBefore = AdminOperationalIndexMetadata.Create(discovery, ["sample"], [query], [projection],
            "sample-app", "v1", new(), keyedProcessorDomains: ["sample"]);

        var legacy = AdminOperationalIndexMetadata.Create(discovery, ["SAMPLE", "sample"], [query], ["SAMPLE", "sample"]);
        AdminOperationalIndexMetadata.DomainMetadata domain = legacy.Domains.ShouldHaveSingleItem();
        domain.Domain.ShouldBe("sample"); domain.QueryTypes.ShouldBe(["get-sample"]);
        domain.NamedProjectionTypes.ShouldBeNull(); legacy.DispatchCapability.ShouldBeNull();
        legacy.CatalogFingerprint.ShouldBeNull();

        var namedAfter = AdminOperationalIndexMetadata.Create(discovery, ["sample"], [query], [projection],
            "sample-app", "v1", new(), keyedProcessorDomains: ["sample"]);
        JsonSerializer.Serialize(namedAfter).ShouldBe(JsonSerializer.Serialize(namedBefore));
        namedAfter.Domains.ShouldHaveSingleItem().NamedProjectionTypes.ShouldBe(["sample-index"]);
        namedAfter.CatalogFingerprint.ShouldBe(ProjectionRouteCatalogFingerprint.Compute("sample-app", "v1",
            [new ProjectionDispatchRoute("sample", "sample-index")]));
    }

    /// <summary>Null and empty legacy domain lists retain aggregate discovery without fabricating keyed routes.</summary>
    /// <param name="nullDomains">Whether the legacy request omits its domain list.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LegacyMetadataNullOrEmptyDomainsRetainAggregateDiscovery(bool nullDomains) {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        _ = builder.AddEventStoreDomainService(typeof(WidgetAggregate).Assembly);
        _ = builder.Services.AddKeyedScoped<IDomainProcessor>("keyed-only", static (_, _) => Substitute.For<IDomainProcessor>());
        await using WebApplication app = builder.Build();
        _ = app.UseEventStoreDomainService();
        (int status, string body) = await InvokeEndpointAsync(app, "/admin/operational-index-metadata",
            new AdminOperationalIndexMetadata.Request(nullDomains ? null! : []));
        status.ShouldBe(StatusCodes.Status200OK, body);
        AdminOperationalIndexMetadata.Response response = JsonSerializer.Deserialize<AdminOperationalIndexMetadata.Response>(body,
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        response.Domains.Select(domain => domain.Domain).ShouldContain("widget");
        response.Domains.Select(domain => domain.Domain).ShouldNotContain("keyed-only");
        response.DispatchVersion.ShouldBeNull();
        response.CatalogFingerprint.ShouldBeNull();
    }

    /// <summary>Invalid requested names cannot turn an unkeyed processor into a discovered domain.</summary>
    [Fact]
    public async Task LegacyMetadataInvalidNamesCannotUseUnkeyedProcessor() {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        _ = builder.AddEventStoreDomainService(typeof(WidgetAggregate).Assembly);
        _ = builder.Services.AddScoped(_ => Substitute.For<IDomainProcessor>());
        await using WebApplication app = builder.Build();
        _ = app.UseEventStoreDomainService();
        (int status, string body) = await InvokeEndpointAsync(app, "/admin/operational-index-metadata",
            new AdminOperationalIndexMetadata.Request([null!, "", " ", "unknown"]));
        status.ShouldBe(StatusCodes.Status200OK, body);
        AdminOperationalIndexMetadata.Response response = JsonSerializer.Deserialize<AdminOperationalIndexMetadata.Response>(body,
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        response.Domains.ShouldBeEmpty();
        response.DispatchVersion.ShouldBeNull();
    }

    /// <summary>
    /// Proves <c>IDomainQueryHandler</c> implementations in the domain assembly are discovered and registered.
    /// </summary>
    [Fact]
    public void AddEventStoreDomainService_RegistersDiscoveredQueryHandlers() {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        _ = builder.AddEventStoreDomainService();
        using ServiceProvider provider = builder.Services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();

        IEnumerable<IDomainQueryHandler> handlers = scope.ServiceProvider.GetServices<IDomainQueryHandler>();
        handlers.ShouldContain(h => h.Domain == "widget" && h.QueryType == "get-widget");
    }

    /// <summary>
    /// Proves duplicate query routes fail during SDK endpoint setup, before the first routed query.
    /// </summary>
    [Fact]
    public void MapEventStoreDomainService_ThrowsWhenDuplicateQueryHandlersAreRegistered() {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        _ = builder.AddEventStoreDomainService();
        _ = builder.Services.AddScoped<IDomainQueryHandler, WidgetQueryHandler>();
        WebApplication app = builder.Build();

        InvalidOperationException ex = Should.Throw<InvalidOperationException>(() => app.MapEventStoreDomainService());

        ex.Message.ShouldContain("Duplicate query handlers are registered");
        ex.Message.ShouldContain(nameof(WidgetQueryHandler));
    }

    /// <summary>
    /// Proves duplicate projection domains fail during SDK endpoint setup, before first-match dispatch can
    /// hide one of the handlers.
    /// </summary>
    [Fact]
    public void MapEventStoreDomainService_ThrowsWhenDuplicateProjectionHandlersAreRegistered() {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        _ = builder.AddEventStoreDomainService();
        IDomainProjectionHandler duplicateProjection = CreateDuplicateWidgetProjectionHandler();
        _ = builder.Services.AddSingleton(duplicateProjection);
        WebApplication app = builder.Build();

        InvalidOperationException ex = Should.Throw<InvalidOperationException>(() => app.MapEventStoreDomainService());

        ex.Message.ShouldContain("Duplicate projection handlers are registered");
        ex.Message.ShouldContain(nameof(WidgetProjection));
        ex.Message.ShouldContain("widget");
    }

    /// <summary>
    /// Proves the <c>/query</c> dispatch path routes to the handler matching the envelope's domain + query type.
    /// </summary>
    [Fact]
    public async Task DomainQueryDispatcher_Execute_RoutesToMatchingHandler() {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        _ = builder.AddEventStoreDomainService();
        using ServiceProvider provider = builder.Services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();

        QueryEnvelope query = new("test-tenant", "widget", "widget-1", "get-widget", [], "corr-1", "test-user");

        QueryResult result = await DomainQueryDispatcher.ExecuteAsync(scope.ServiceProvider, query);

        result.Success.ShouldBeTrue();
        result.GetPayload().GetProperty("aggregateId").GetString().ShouldBe("widget-1");
        _ = result.Metadata.ShouldNotBeNull();
        result.Metadata.IsStale.ShouldBe(false);
        result.Metadata.ProjectionVersion.ShouldBe("widget-projection-v1");
        _ = result.Metadata.Paging.ShouldNotBeNull();
        result.Metadata.Paging.PageSize.ShouldBe(10);
    }

    [Fact]
    public async Task DomainQueryDispatcher_Execute_ForwardsAndObservesOriginalCancellation() {
        using var cancellation = new CancellationTokenSource();
        IDomainQueryHandler handler = Substitute.For<IDomainQueryHandler>();
        handler.Domain.Returns("widget");
        handler.QueryType.Returns("get-widget");
        CancellationToken observed = default;
        handler.ExecuteAsync(Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>()).Returns(call => {
            observed = call.ArgAt<CancellationToken>(1);
            cancellation.Cancel();
            return Task.FromResult(QueryResult.FromPayload(JsonSerializer.SerializeToElement(new { value = 1 })));
        });
        var services = new ServiceCollection();
        services.AddSingleton(handler);
        using ServiceProvider provider = services.BuildServiceProvider();
        QueryEnvelope query = new("test-tenant", "widget", "widget-1", "get-widget", [], "corr-1", "test-user");

        OperationCanceledException exception = await Should.ThrowAsync<OperationCanceledException>(
            () => DomainQueryDispatcher.ExecuteAsync(provider, query, cancellation.Token));

        observed.ShouldBe(cancellation.Token);
        exception.CancellationToken.ShouldBe(cancellation.Token);
    }

    /// <summary>
    /// Proves the dispatcher returns a failure result when no handler matches the query.
    /// </summary>
    [Fact]
    public async Task DomainQueryDispatcher_Execute_ReturnsFailure_WhenNoHandlerMatches() {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        _ = builder.AddEventStoreDomainService();
        using ServiceProvider provider = builder.Services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();

        QueryEnvelope query = new("test-tenant", "widget", "widget-1", "nonexistent-query", [], "corr-1", "test-user");

        QueryResult result = await DomainQueryDispatcher.ExecuteAsync(scope.ServiceProvider, query);

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldContain("No query handler is registered");
    }

    /// <summary>
    /// Proves duplicate query routes are rejected deterministically instead of first-match dispatching.
    /// </summary>
    [Fact]
    public async Task DomainQueryDispatcher_Execute_ThrowsWhenDuplicateHandlersMatch() {
        var services = new ServiceCollection();
        _ = services.AddScoped<IDomainQueryHandler, WidgetQueryHandler>();
        _ = services.AddScoped<IDomainQueryHandler, WidgetQueryHandler>();
        using ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        QueryEnvelope query = new("test-tenant", "widget", "widget-1", "get-widget", [], "corr-1", "test-user");

        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(
            () => DomainQueryDispatcher.ExecuteAsync(scope.ServiceProvider, query));

        ex.Message.ShouldContain("Duplicate query handlers are registered");
        ex.Message.ShouldContain(nameof(WidgetQueryHandler));
    }

    /// <summary>
    /// Proves operational metadata rejects ambiguous handler routes before the gateway materializes indexes.
    /// </summary>
    [Fact]
    public void AdminOperationalIndexMetadata_Create_ThrowsWhenDuplicateQueryHandlersMatch() {
        DiscoveryResult discovery = new(
            [new DiscoveredDomain(typeof(WidgetAggregate), "widget", typeof(WidgetState), DomainKind.Aggregate)],
            []);

        InvalidOperationException ex = Should.Throw<InvalidOperationException>(
            () => AdminOperationalIndexMetadata.Create(
                discovery,
                ["widget"],
                [new WidgetQueryHandler(), new WidgetQueryHandler()]));

        ex.Message.ShouldContain("Duplicate query handlers are registered");
        ex.Message.ShouldContain(nameof(WidgetQueryHandler));
    }

    /// <summary>
    /// Proves <c>IDomainProjectionHandler</c> implementations in the domain assembly are discovered and
    /// registered as singletons so the SDK's <c>/project</c> endpoint can resolve them (Epic A3).
    /// </summary>
    [Fact]
    public void AddEventStoreDomainService_RegistersDiscoveredProjectionHandlers() {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        _ = builder.AddEventStoreDomainService();
        using ServiceProvider provider = builder.Services.BuildServiceProvider();

        // Resolved from the root provider (not a scope) — proves singleton registration.
        IEnumerable<IDomainProjectionHandler> handlers = provider.GetServices<IDomainProjectionHandler>();
        handlers.ShouldContain(h => h.Domain == "widget");
    }

    /// <summary>
    /// Proves the <c>/project</c> dispatch path routes to the handler matching the request's domain.
    /// </summary>
    [Fact]
    public void DomainProjectionDispatcher_Project_RoutesToMatchingHandler() {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        _ = builder.AddEventStoreDomainService();
        using ServiceProvider provider = builder.Services.BuildServiceProvider();

        ProjectionRequest request = new("test-tenant", "widget", "widget-1", [CreateProjectionEvent(), CreateProjectionEvent()]);

        ProjectionResponse? response = DomainProjectionDispatcher.Project(provider, request);

        response.ShouldNotBeNull();
        response.ProjectionType.ShouldBe("widget");
        response.State.GetProperty("count").GetInt32().ShouldBe(2);
    }

    /// <summary>
    /// Proves the dispatcher returns <c>null</c> (mapped to <c>404</c> by the endpoint) when no handler matches
    /// the request's domain.
    /// </summary>
    [Fact]
    public void DomainProjectionDispatcher_Project_ReturnsNull_WhenNoHandlerMatches() {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        _ = builder.AddEventStoreDomainService();
        using ServiceProvider provider = builder.Services.BuildServiceProvider();

        ProjectionRequest request = new("test-tenant", "unknown-domain", "x-1", []);

        ProjectionResponse? response = DomainProjectionDispatcher.Project(provider, request);

        response.ShouldBeNull();
    }

    /// <summary>
    /// Proves the projection dispatcher validates duplicate domains on direct dispatch as well as endpoint
    /// setup, avoiding accidental first-match behavior in tests and bespoke hosts.
    /// </summary>
    [Fact]
    public void DomainProjectionDispatcher_Project_ThrowsWhenDuplicateHandlersMatch() {
        var services = new ServiceCollection();
        _ = services.AddSingleton<IDomainProjectionHandler, WidgetProjection>();
        IDomainProjectionHandler duplicateProjection = CreateDuplicateWidgetProjectionHandler();
        _ = services.AddSingleton(duplicateProjection);
        using ServiceProvider provider = services.BuildServiceProvider();

        ProjectionRequest request = new("test-tenant", "WIDGET", "widget-1", []);

        InvalidOperationException ex = Should.Throw<InvalidOperationException>(
            () => DomainProjectionDispatcher.Project(provider, request));

        ex.Message.ShouldContain("Duplicate projection handlers are registered");
        ex.Message.ShouldContain(nameof(WidgetProjection));
    }

    /// <summary>
    /// Proves the existing canonical endpoints are retained alongside additive source-guard proof and recovery routes.
    /// </summary>
    [Fact]
    public void MapEventStoreDomainService_MapsCanonicalEndpointsUnchanged() {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        _ = builder.Services.AddSingleton(new DiscoveryResult([], []));
        _ = builder.Services.AddEventStoreDomainServiceSecurity();
        WebApplication app = builder.Build();

        _ = app.MapEventStoreDomainService();

        string[] routes = GetMappedRoutes(app);

        routes.ShouldBe([
            "/",
            "/admin/operational-index-metadata",
            "/process",
            "/project",
            "/project/rebuild/abort/v1",
            "/project/rebuild/commit/v1",
            "/project/rebuild/part/v1",
            "/project/rebuild/reclaim-guarded/v1",
            "/project/rebuild/reclaim-recovery/v1",
            "/project/rebuild/shared/v1",
            "/project/rebuild/stage/v1",
            "/project/rebuild/v1",
            "/project/rebuild/verify/v1",
            "/project/source-guard/applied-receipt/v1",
            "/project/source-guard/owner-proof/v1",
            "/project/source-guard/reconcile-applied-receipt/v1",
            "/project/v2",
            "/project/v2/reconcile",
            "/query",
            "/replay-state"]);
    }

    /// <summary>
    /// Story 5.5 route inventory: on a consuming domain-service host every non-probe endpoint requires credentials,
    /// each canonical operational route carries exactly its catalog policy, sidecar-originated routes require the
    /// app-channel policy, and only <c>/health</c>, <c>/alive</c>, and <c>/ready</c> are anonymous.
    /// </summary>
    [Fact]
    public void UseEventStoreDomainService_EveryNonProbeEndpointRequiresCredentials() {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        _ = builder.AddEventStoreDomainService();
        _ = builder.Services.AddEventStoreDomainEvents(
            typeof(EventStoreDomainServiceExtensionsTests).Assembly,
            options => options.SubscriptionRoute = "/widget/events");
        WebApplication app = builder.Build();

        _ = app.UseEventStoreDomainService();

        RouteEndpoint[] endpoints = GetRouteEndpoints(app);
        endpoints
            .Where(static endpoint => endpoint.Metadata.GetMetadata<Microsoft.AspNetCore.Authorization.IAllowAnonymous>() is not null)
            .Select(static endpoint => endpoint.RoutePattern.RawText)
            .OrderBy(static route => route, StringComparer.Ordinal)
            .ShouldBe(["/alive", "/health", "/ready"]);
        foreach (EventStoreDomainServiceRoute catalogRoute in EventStoreDomainServiceRoutes.Operational) {
            RouteEndpoint endpoint = endpoints.Single(candidate => string.Equals(candidate.RoutePattern.RawText, catalogRoute.Route, StringComparison.Ordinal));
            endpoint.Metadata.GetOrderedMetadata<Microsoft.AspNetCore.Authorization.IAuthorizeData>()
                .Select(static data => data.Policy)
                .ShouldBe([catalogRoute.Policy], catalogRoute.Route);
        }

        foreach (string sidecarRoute in new[] { "/widget/events", "dapr/subscribe" }) {
            RouteEndpoint endpoint = endpoints.Single(candidate => string.Equals(candidate.RoutePattern.RawText, sidecarRoute, StringComparison.Ordinal));
            endpoint.Metadata.GetOrderedMetadata<Microsoft.AspNetCore.Authorization.IAuthorizeData>()
                .Select(static data => data.Policy)
                .ShouldBe([EventStoreDomainServicePolicies.SidecarChannel], sidecarRoute);
        }

        EventStoreDomainServiceEndpointInventory.Validate(endpoints, fallbackPolicy: ServiceDefaults.Authentication.EventStoreWorkloadAuthenticationExtensions.CreateAnyWorkloadPolicy(ServiceDefaults.Authentication.EventStoreWorkloadAuthenticationDefaults.WorkloadScheme)).ShouldBeEmpty();
        Microsoft.AspNetCore.Authorization.AuthorizationPolicy fallback = app.Services
            .GetRequiredService<Microsoft.Extensions.Options.IOptions<Microsoft.AspNetCore.Authorization.AuthorizationOptions>>()
            .Value.FallbackPolicy.ShouldNotBeNull();
        fallback.AuthenticationSchemes.ShouldBe([ServiceDefaults.Authentication.EventStoreWorkloadAuthenticationDefaults.SidecarChannelScheme]);
        fallback.Requirements.ShouldContain(requirement => requirement.GetType().Name == "EventStoreUnclassifiedEndpointRequirement");
    }

    /// <summary>The published policy names are exactly the catalog's operation policies.</summary>
    [Fact]
    public void DomainServicePolicyNames_MatchTheWorkloadOperationPolicies() {
        foreach (EventStoreDomainServiceRoute route in EventStoreDomainServiceRoutes.Operational) {
            route.Policy.ShouldBe(ServiceDefaults.Authentication.EventStoreWorkloadAuthenticationDefaults.OperationPolicy(
                ServiceDefaults.Authentication.EventStoreWorkloadAuthenticationDefaults.WorkloadScheme,
                route.Operation));
        }

        EventStoreDomainServicePolicies.AnyWorkload.ShouldBe(
            ServiceDefaults.Authentication.EventStoreWorkloadAuthenticationDefaults.AnyWorkloadPolicy(
                ServiceDefaults.Authentication.EventStoreWorkloadAuthenticationDefaults.WorkloadScheme));
    }

    /// <summary>
    /// Story 5.5 route override: a host override of an SDK route that drops or weakens the policy, or any extra
    /// anonymous endpoint, is an inventory violation; an override carrying the exact policy is compliant.
    /// </summary>
    [Fact]
    public void EndpointInventory_RejectsWeakOverridesAndExtraAnonymousEndpoints() {
        WebApplication weak = BuildWithOverride(app => app.MapPost("/project", () => "weak"));
        WebApplication anonymousExtra = BuildWithOverride(app => app.MapGet("/debug", () => "open").AllowAnonymous());
        WebApplication wrongPolicy = BuildWithOverride(app => app.MapPost("/project/v2", () => "wrong")
            .RequireAuthorization(EventStoreDomainServicePolicies.Query));
        WebApplication secured = BuildWithOverride(app => app.MapPost("/project", () => "secured")
            .RequireAuthorization(EventStoreDomainServicePolicies.Project));

        EventStoreDomainServiceEndpointInventory.Validate(GetRouteEndpoints(weak), fallbackPolicy: ServiceDefaults.Authentication.EventStoreWorkloadAuthenticationExtensions.CreateAnyWorkloadPolicy(ServiceDefaults.Authentication.EventStoreWorkloadAuthenticationDefaults.WorkloadScheme))
            .ShouldHaveSingleItem().ShouldContain("/project");
        EventStoreDomainServiceEndpointInventory.Validate(GetRouteEndpoints(anonymousExtra), fallbackPolicy: ServiceDefaults.Authentication.EventStoreWorkloadAuthenticationExtensions.CreateAnyWorkloadPolicy(ServiceDefaults.Authentication.EventStoreWorkloadAuthenticationDefaults.WorkloadScheme))
            .ShouldHaveSingleItem().ShouldContain("/debug");
        EventStoreDomainServiceEndpointInventory.Validate(GetRouteEndpoints(wrongPolicy), fallbackPolicy: ServiceDefaults.Authentication.EventStoreWorkloadAuthenticationExtensions.CreateAnyWorkloadPolicy(ServiceDefaults.Authentication.EventStoreWorkloadAuthenticationDefaults.WorkloadScheme))
            .ShouldHaveSingleItem().ShouldContain("/project/v2");
        EventStoreDomainServiceEndpointInventory.Validate(GetRouteEndpoints(secured), fallbackPolicy: ServiceDefaults.Authentication.EventStoreWorkloadAuthenticationExtensions.CreateAnyWorkloadPolicy(ServiceDefaults.Authentication.EventStoreWorkloadAuthenticationDefaults.WorkloadScheme))
            .ShouldBeEmpty();

        static WebApplication BuildWithOverride(Action<WebApplication> mapOverride) {
            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            _ = builder.AddEventStoreDomainService();
            WebApplication app = builder.Build();
            mapOverride(app);
            _ = app.UseEventStoreDomainService();
            return app;
        }
    }

    /// <summary>A GET-only canonical route requires its exact policy and cannot rely on fallback or public markers.</summary>
    /// <param name="publicConfiguration">The public metadata applied to the GET route.</param>
    [Theory]
    [InlineData("none")]
    [InlineData("anonymous")]
    [InlineData("marker")]
    public void EndpointInventory_GetOnlyCanonicalRouteRequiresPrivateAccess(string publicConfiguration) {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        _ = builder.AddEventStoreDomainService();
        WebApplication app = builder.Build();
        RouteHandlerBuilder route = app.MapGet("/project", () => "diagnostic view");
        if (publicConfiguration == "anonymous") {
            _ = route.AllowAnonymous();
        }
        else if (publicConfiguration == "marker") {
            _ = route.AllowEventStorePublicEndpoint("/project");
        }

        _ = app.UseEventStoreDomainService();
        Microsoft.AspNetCore.Authorization.AuthorizationPolicy fallback = ServiceDefaults.Authentication.EventStoreWorkloadAuthenticationExtensions
            .CreateAnyWorkloadPolicy(ServiceDefaults.Authentication.EventStoreWorkloadAuthenticationDefaults.WorkloadScheme);
        IReadOnlyList<string> violations = EventStoreDomainServiceEndpointInventory.Validate(GetRouteEndpoints(app), fallback);
        violations.ShouldContain($"/project: the effective endpoint must require policy '{EventStoreDomainServicePolicies.Project}'.");
        if (publicConfiguration != "none") {
            violations.ShouldContain("/project: a canonical route cannot be public.");
        }
    }

    /// <summary>A GET-only canonical route with explicit authorization must use its catalog policy.</summary>
    /// <param name="authorization">The authorization applied to the GET route.</param>
    [Theory]
    [InlineData("default")]
    [InlineData("wrong-policy")]
    [InlineData("project-policy")]
    public void EndpointInventory_GetOnlyCanonicalRouteRejectsWeakExplicitAuthorization(string authorization) {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        _ = builder.AddEventStoreDomainService();
        WebApplication app = builder.Build();
        RouteHandlerBuilder route = app.MapGet("/project", () => "diagnostic view");
        if (authorization == "default") {
            _ = route.RequireAuthorization();
        }
        else if (authorization == "wrong-policy") {
            _ = route.RequireAuthorization(EventStoreDomainServicePolicies.Query);
        }
        else {
            _ = route.RequireAuthorization(EventStoreDomainServicePolicies.Project);
        }

        _ = app.UseEventStoreDomainService();
        Microsoft.AspNetCore.Authorization.AuthorizationPolicy fallback = ServiceDefaults.Authentication.EventStoreWorkloadAuthenticationExtensions
            .CreateAnyWorkloadPolicy(ServiceDefaults.Authentication.EventStoreWorkloadAuthenticationDefaults.WorkloadScheme);
        IReadOnlyList<string> violations = EventStoreDomainServiceEndpointInventory.Validate(GetRouteEndpoints(app), fallback);
        if (authorization == "project-policy") {
            violations.ShouldBeEmpty();
        }
        else {
            violations.ShouldHaveSingleItem().ShouldContain($"/project: the effective endpoint must require policy '{EventStoreDomainServicePolicies.Project}'.");
        }
    }
    /// <summary>Explicit public metadata cannot create anonymous exceptions on conforming hosts.</summary>
    /// <param name="route">The literal public route.</param>
    /// <param name="method">Its single HTTP method.</param>
    [Theory]
    [InlineData("/metadata/timesheets", "GET")]
    [InlineData("/api/timesheets/magic-links/confirm", "GET")]
    [InlineData("/api/timesheets/magic-links/confirm/submit", "POST")]
    [InlineData("/api/timesheets/magic-links/adjust", "GET")]
    [InlineData("/api/timesheets/magic-links/adjust/submit", "POST")]
    public void EndpointInventory_RejectsExplicitPublicRoutes(string route, string method) {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        _ = builder.AddEventStoreDomainService();
        WebApplication app = builder.Build();
        if (method == HttpMethods.Get) {
            _ = app.MapGet(route, () => "public").AllowEventStorePublicEndpoint(route);
        }
        else {
            _ = app.MapPost(route, () => "public").AllowEventStorePublicEndpoint(route);
        }

        _ = app.UseEventStoreDomainService();
        EventStoreDomainServiceEndpointInventory.Validate(GetRouteEndpoints(app), fallbackPolicy: null)
            .ShouldHaveSingleItem().ShouldContain("only exact health, alive, and ready probes may be anonymous.");
    }

    /// <summary>A marker cannot make a bare, protected, mismatched, or broad route public.</summary>
    /// <param name="scenario">The invalid endpoint configuration.</param>
    [Theory]
    [InlineData("bare")]
    [InlineData("canonical")]
    [InlineData("sidecar")]
    [InlineData("mismatched")]
    [InlineData("template")]
    [InlineData("all-methods")]
    [InlineData("multi-method")]
    [InlineData("group")]
    [InlineData("mixed-authorization")]
    [InlineData("duplicate-marker")]
    public void EndpointInventory_RejectsInvalidPublicOptIns(string scenario) {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        _ = builder.AddEventStoreDomainService();
        WebApplication app = builder.Build();
        switch (scenario) {
            case "bare":
                _ = app.MapGet("/public", () => "open").AllowAnonymous();
                break;
            case "canonical":
                _ = app.MapPost("/project/v2", () => "open")
                    .RequireAuthorization(EventStoreDomainServicePolicies.Project)
                    .AllowEventStorePublicEndpoint("/project/v2");
                break;
            case "sidecar":
                _ = app.MapPost("/widget/events", () => "open")
                    .WithTopic("pubsub", "widget.events")
                    .RequireEventStoreSidecarChannel()
                    .AllowEventStorePublicEndpoint("/widget/events");
                break;
            case "mismatched":
                _ = app.MapGet("/public", () => "open").AllowEventStorePublicEndpoint("/other");
                break;
            case "template":
                _ = app.MapGet("/public/{id}", (string id) => id).AllowEventStorePublicEndpoint("/public/{id}");
                break;
            case "all-methods":
                _ = app.Map("/public", () => "open").AllowEventStorePublicEndpoint("/public");
                break;
            case "multi-method":
                _ = app.MapMethods("/public", [HttpMethods.Get, HttpMethods.Post], () => "open")
                    .AllowEventStorePublicEndpoint("/public");
                break;
            case "mixed-authorization":
                _ = app.MapGet("/public", () => "open")
                    .RequireAuthorization(EventStoreDomainServicePolicies.AnyWorkload)
                    .AllowEventStorePublicEndpoint("/public");
                break;
            case "duplicate-marker":
                _ = app.MapGet("/public", () => "open")
                    .AllowEventStorePublicEndpoint("/public")
                    .AllowEventStorePublicEndpoint("/public");
                break;
            default:
                _ = app.MapGroup("/public").AllowEventStorePublicEndpoint("/public")
                    .MapGet("/child", () => "open");
                break;
        }

        _ = app.UseEventStoreDomainService();
        EventStoreDomainServiceEndpointInventory.Validate(GetRouteEndpoints(app), fallbackPolicy: null)
            .ShouldNotBeEmpty(scenario);
    }

    /// <summary>Without a fallback policy every endpoint lacking authorization metadata is a violation.</summary>
    [Fact]
    public void EndpointInventory_WithoutFallbackPolicy_RequiresExplicitMetadata() {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        _ = builder.AddEventStoreDomainService();
        WebApplication app = builder.Build();
        _ = app.MapGet("/unprotected", () => "x");
        _ = app.UseEventStoreDomainService();

        EventStoreDomainServiceEndpointInventory.Validate(GetRouteEndpoints(app), fallbackPolicy: null)
            .ShouldHaveSingleItem().ShouldContain("/unprotected");
    }

    /// <summary>
    /// Story 5.5 (P-7): a fallback policy counts only when it denies anonymous callers; one that admits them protects
    /// nothing, so endpoints without metadata are still violations.
    /// </summary>
    [Fact]
    public void EndpointInventory_NeverClassifiesEndpointsUsingFallback() {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        _ = builder.AddEventStoreDomainService();
        WebApplication app = builder.Build();
        _ = app.MapGet("/unprotected", () => "x");
        _ = app.UseEventStoreDomainService();
        Microsoft.AspNetCore.Authorization.AuthorizationPolicy admitsAnonymous = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
            .RequireAssertion(static _ => true)
            .Build();
        Microsoft.AspNetCore.Authorization.AuthorizationPolicy deniesAnonymous = ServiceDefaults.Authentication.EventStoreWorkloadAuthenticationExtensions
            .CreateAnyWorkloadPolicy(ServiceDefaults.Authentication.EventStoreWorkloadAuthenticationDefaults.WorkloadScheme);

        EventStoreDomainServiceEndpointInventory.DeniesAnonymous(admitsAnonymous).ShouldBeFalse();
        EventStoreDomainServiceEndpointInventory.DeniesAnonymous(null).ShouldBeFalse();
        EventStoreDomainServiceEndpointInventory.DeniesAnonymous(deniesAnonymous).ShouldBeTrue();
        EventStoreDomainServiceEndpointInventory.Validate(GetRouteEndpoints(app), admitsAnonymous)
            .ShouldHaveSingleItem().ShouldContain("/unprotected");
        EventStoreDomainServiceEndpointInventory.Validate(GetRouteEndpoints(app), deniesAnonymous)
            .ShouldHaveSingleItem().ShouldContain("/unprotected");
    }

    /// <summary>
    /// Story 5.5 (P-3): a host that maps subscription discovery, a pub/sub subscription, or the Dapr actor routes
    /// itself must attach the sidecar-channel policy; falling through to the any-workload fallback would deny every
    /// sidecar delivery silently, so the inventory reports it.
    /// </summary>
    /// <param name="premapped">What the host maps before the SDK.</param>
    /// <param name="expectedRoute">A route the violation names, or <see langword="null"/> when compliant.</param>
    [Theory]
    [InlineData("subscribe", "/dapr/subscribe")]
    [InlineData("subscription", "/widget/premapped-events")]
    [InlineData("actors", "/actors/")]
    [InlineData("secured", null)]
    public void EndpointInventory_RequiresTheSidecarChannelPolicyOnSidecarRoutes(string premapped, string? expectedRoute) {
        ArgumentNullException.ThrowIfNull(premapped);
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        _ = builder.AddEventStoreDomainService();
        builder.Services.AddActors(configure: null);
        WebApplication app = builder.Build();
        switch (premapped) {
            case "subscribe":
                _ = app.MapSubscribeHandler();
                break;
            case "subscription":
                _ = app.MapPost("/widget/premapped-events", () => Results.Ok()).WithTopic("pubsub", "widget.events");
                break;
            case "actors":
                _ = app.MapActorsHandlers();
                break;
            default:
                _ = app.MapSubscribeHandler().RequireEventStoreSidecarChannel();
                _ = app.MapPost("/widget/premapped-events", () => Results.Ok())
                    .WithTopic("pubsub", "widget.events")
                    .RequireEventStoreSidecarChannel();
                _ = app.MapActorsHandlers().RequireEventStoreSidecarChannel();
                break;
        }

        _ = app.UseEventStoreDomainService();

        IReadOnlyList<string> violations = EventStoreDomainServiceEndpointInventory.Validate(
            GetRouteEndpoints(app),
            ServiceDefaults.Authentication.EventStoreWorkloadAuthenticationExtensions.CreateAnyWorkloadPolicy(
                ServiceDefaults.Authentication.EventStoreWorkloadAuthenticationDefaults.WorkloadScheme));
        if (expectedRoute is null) {
            violations.ShouldBeEmpty();
            return;
        }

        violations.ShouldNotBeEmpty();
        violations.ShouldAllBe(static violation => violation.Contains(EventStoreDomainServicePolicies.SidecarChannel, StringComparison.Ordinal)
            || violation.Contains("anonymous", StringComparison.Ordinal));
        violations.ShouldContain(violation => violation.Contains(expectedRoute, StringComparison.Ordinal));
    }

    [Fact]
    public async Task MetadataNegotiation_OldRequestRetainsOldFingerprintAndCompatibleDispatchWithNewHandler()
    {
        const string domain = "guard-probe";
        const string route = "guard-summary";
        IAsyncDomainProjectionSingleEventHandler handler = Substitute.For<IAsyncDomainProjectionSingleEventHandler,
            IAsyncDomainProjectionRebuildHandler>();
        ((IAsyncDomainProjectionRebuildHandler)handler).RebuildSemantics.Returns(DomainProjectionRebuildSemantics.FullReplay);
        handler.Domain.Returns(domain);
        handler.ProjectionType.Returns(route);
        handler.OrdinaryDeliveryEnabled.Returns(true);
        handler.ProjectAsync(Arg.Any<ProjectionRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(DomainProjectionHandlerResult.Completed());
        var capability = new ProjectionSourceGuardCapability(1, 1, 1, "single-event-v1", "projection-owner-proof/v1",
            "event-message-id/v1", "read-model-batch/v1") { CanonicalReplayProtocol = "staged-full-replay/v1" };
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Configuration["EventStore:DomainService:AppId"] = "sample";
        builder.Configuration["EventStore:DomainService:ServiceVersion"] = "v1";
        _ = builder.AddEventStoreDomainService();
        _ = builder.Services.AddSingleton<IAsyncDomainProjectionHandler>(handler);
        _ = builder.Services.AddKeyedSingleton<IDomainProcessor>(domain, Substitute.For<IDomainProcessor>());
        _ = builder.Services.AddSingleton(new DomainProjectionOwnerProofRegistration(domain, route, capability, new("models", route)));
        await using WebApplication app = builder.Build();
        _ = app.UseEventStoreDomainService();
        // This body has exactly the old server's fields, with no capability negotiation.
        var oldRequest = new AdminOperationalIndexMetadata.Request([domain]) { AppId = "sample", ServiceVersion = "v1" };
        (int oldStatus, string oldBody) = await InvokeEndpointAsync(app, "/admin/operational-index-metadata", oldRequest);
        oldStatus.ShouldBe(StatusCodes.Status200OK, oldBody);
        AdminOperationalIndexMetadata.Response oldResponse = JsonSerializer.Deserialize<AdminOperationalIndexMetadata.Response>(oldBody,
            JsonSerializerOptions.Web).ShouldNotBeNull();
        AdminOperationalIndexMetadata.DomainMetadata oldDomain = oldResponse.Domains.Single();
        oldDomain.SourceGuardCapabilities.ShouldBeNull();
        oldDomain.SingleEventProjectionTypes.ShouldNotBeNull().ShouldContain(route);
        oldDomain.FullReplayProjectionTypes.ShouldNotBeNull().ShouldNotContain(route);
        // The pre-feature algorithm knows only the original route fields. Its exact
        // recomputation must still accept the issued response from this new handler.
        string oldFingerprint = ProjectionRouteCatalogFingerprint.Compute("sample", "v1",
            oldDomain.NamedProjectionTypes!.Select(type => new ProjectionDispatchRoute(domain, type)),
            oldDomain.OrdinaryProjectionTypes!.Select(type => new ProjectionDispatchRoute(domain, type)));
        oldResponse.CatalogFingerprint.ShouldBe(oldFingerprint);
        var dispatch = new ProjectionDispatchRequest(new("tenant-a", domain, "owner", []), [route], "old-dispatch", oldFingerprint);
        (int dispatchStatus, _) = await InvokeEndpointAsync(app, "/project/v2", dispatch);
        dispatchStatus.ShouldBe(StatusCodes.Status200OK);
        (await DomainProjectionDispatcher.DispatchAsync(app.Services, dispatch, new ProjectionDispatchOptions(),
            new DomainProjectionIdentityOptions { AppId = "sample", ServiceVersion = "v1" }, CancellationToken.None))
            .Outcomes.Single().Status.ShouldBe(ProjectionDispatchStatus.Completed);

        (int guardStatus, string guardBody) = await InvokeEndpointAsync(app, "/admin/operational-index-metadata",
            oldRequest with { ProjectionSourceGuardVersion = 1 });
        guardStatus.ShouldBe(StatusCodes.Status200OK, guardBody);
        AdminOperationalIndexMetadata.Response guarded = JsonSerializer.Deserialize<AdminOperationalIndexMetadata.Response>(guardBody,
            JsonSerializerOptions.Web).ShouldNotBeNull();
        guarded.CatalogFingerprint.ShouldNotBe(oldFingerprint);
        guarded.Domains.Single().SourceGuardCapabilities.ShouldNotBeNull()[route].ShouldBe(capability);
        DomainProjectionCatalogRegistry registry = app.Services.GetRequiredService<DomainProjectionCatalogRegistry>();
        registry.AuthorizesSourceGuard(oldFingerprint, domain, route, capability).ShouldBeFalse();
        registry.AuthorizesSourceGuard(guarded.CatalogFingerprint!, domain, route, capability).ShouldBeTrue();
        (int invalidStatus, _) = await InvokeEndpointAsync(app, "/admin/operational-index-metadata",
            oldRequest with { ProjectionSourceGuardVersion = 2 });
        invalidStatus.ShouldBe(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task NamedProjectionEndpoints_RegisterCatalogAndMapValidationFailureToBadRequest() {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Configuration["EventStore:DomainService:AppId"] = "sample";
        builder.Configuration["EventStore:DomainService:ServiceVersion"] = "v1";
        _ = builder.AddEventStoreDomainService();
        _ = builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);
        WebApplication app = builder.Build();
        _ = app.UseEventStoreDomainService();
        string fingerprint = ProjectionRouteCatalogFingerprint.Compute(
            "sample",
            "v1",
            [
                new ProjectionDispatchRoute("widget", "widget-detail"),
            ]);
        var metadataRequest = new AdminOperationalIndexMetadata.Request(["widget"]) {
            AppId = "sample",
            ServiceVersion = "v1",
        };

        (int metadataStatus, string metadataBody) = await InvokeEndpointAsync(
            app,
            "/admin/operational-index-metadata",
            metadataRequest);

        metadataStatus.ShouldBe(StatusCodes.Status200OK, metadataBody);
        DomainProjectionCatalogRegistry catalogRegistry = app.Services.GetRequiredService<DomainProjectionCatalogRegistry>();
        catalogRegistry.Contains(fingerprint).ShouldBeTrue();
        catalogRegistry.Authorizes(fingerprint, "widget", ["widget-detail"]).ShouldBeTrue();

        var validDispatch = new ProjectionDispatchRequest(
            new ProjectionRequest("tenant-a", "widget", "widget-1", []),
            ["widget-detail"],
            "dispatch-1",
            fingerprint);
        (int validStatus, string validBody) = await InvokeEndpointAsync(app, "/project/v2", validDispatch);
        validStatus.ShouldBe(StatusCodes.Status200OK, validBody);
        validBody.ShouldContain("widget-detail");

        var invalidDispatch = new ProjectionDispatchRequest(
            new ProjectionRequest("tenant-a", "widget", "widget-1", []),
            [],
            "dispatch-1",
            fingerprint);
        (int dispatchStatus, string dispatchBody) = await InvokeEndpointAsync(app, "/project/v2", invalidDispatch);

        dispatchStatus.ShouldBe(StatusCodes.Status400BadRequest);
        dispatchBody.ShouldContain(ProjectionDispatchReasonCodes.MalformedOutcome);
    }

    [Fact]
    public async Task NamedProjectionEndpoint_ForwardsLiveCallerToken() {
        using var cancellation = new CancellationTokenSource();
        CancellationToken observed = default;
        IAsyncDomainProjectionHandler handler = CreateNamedProjectionHandler("token-probe", "token-summary");
        handler.ProjectAsync(Arg.Any<ProjectionRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => {
                observed = call.ArgAt<CancellationToken>(2);
                return DomainProjectionHandlerResult.Completed(JsonSerializer.SerializeToElement(new { count = 1 }));
            });
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Configuration["EventStore:DomainService:AppId"] = "sample";
        builder.Configuration["EventStore:DomainService:ServiceVersion"] = "v1";
        _ = builder.AddEventStoreDomainService();
        _ = builder.Services.AddSingleton(handler);
        WebApplication app = builder.Build();
        _ = app.UseEventStoreDomainService();
        string fingerprint = ProjectionRouteCatalogFingerprint.Compute("sample", "v1",
            [new ProjectionDispatchRoute("token-probe", "token-summary")]);
        var dispatch = new ProjectionDispatchRequest(
            new ProjectionRequest("tenant-a", "token-probe", "probe-1", []),
            ["token-summary"], "dispatch-1", fingerprint);

        (int status, string body) = await InvokeEndpointAsync(app, "/project/v2", dispatch, cancellation.Token);

        status.ShouldBe(StatusCodes.Status200OK, body);
        observed.ShouldBe(cancellation.Token);
        observed.CanBeCanceled.ShouldBeTrue();
    }

    [Fact]
    public async Task ProjectionEndpoint_ForwardsLiveCallerToken() {
        using var cancellation = new CancellationTokenSource();
        CancellationToken observed = default;
        IDomainProjectionHandler handler = Substitute.For<IDomainProjectionHandler>();
        handler.Domain.Returns("token-probe");
        handler.Project(Arg.Any<ProjectionRequest>(), Arg.Any<CancellationToken>())
            .Returns(call => {
                observed = call.ArgAt<CancellationToken>(1);
                return new ProjectionResponse("token-summary", JsonSerializer.SerializeToElement(new { count = 1 }));
            });
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Configuration["EventStore:DomainService:AppId"] = "sample";
        builder.Configuration["EventStore:DomainService:ServiceVersion"] = "v1";
        _ = builder.AddEventStoreDomainService();
        _ = builder.Services.AddSingleton(handler);
        WebApplication app = builder.Build();
        _ = app.UseEventStoreDomainService();
        var request = new ProjectionRequest("tenant-a", "token-probe", "probe-1", []);

        (int status, string body) = await InvokeEndpointAsync(app, "/project", request, cancellation.Token);

        status.ShouldBe(StatusCodes.Status200OK, body);
        observed.ShouldBe(cancellation.Token);
        observed.CanBeCanceled.ShouldBeTrue();
    }

    [Fact]
    public async Task NamedMetadataEndpoint_RejectsCallerSelectedAppIdentity() {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Configuration["EventStore:DomainService:AppId"] = "authoritative-app";
        builder.Configuration["EventStore:DomainService:ServiceVersion"] = "v1";
        _ = builder.AddEventStoreDomainService();
        WebApplication app = builder.Build();
        _ = app.UseEventStoreDomainService();
        var request = new AdminOperationalIndexMetadata.Request(["widget"]) {
            AppId = "attacker-selected-app",
            ServiceVersion = "v1",
        };

        (int status, string body) = await InvokeEndpointAsync(
            app,
            "/admin/operational-index-metadata",
            request);

        status.ShouldBe(StatusCodes.Status400BadRequest, body);
        body.ShouldContain(ProjectionDispatchReasonCodes.UnsupportedCapability);
    }

    [Fact]
    public void UseEventStoreDomainService_InvokesDuplicateNamedRouteValidationAtStartup() {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        _ = builder.AddEventStoreDomainService();
        _ = builder.Services.AddScoped(_ => CreateNamedProjectionHandler("widget", "widget-detail"));
        WebApplication app = builder.Build();

        InvalidOperationException exception = Should.Throw<InvalidOperationException>(() =>
            app.UseEventStoreDomainService());

        exception.Message.ShouldContain(ProjectionDispatchReasonCodes.DuplicateRoute);
    }

    [Fact]
    public void UseEventStoreDomainService_PreservesPreMappedCustomNamedEndpoint() {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        _ = builder.AddEventStoreDomainService();
        WebApplication app = builder.Build();
        _ = app.MapPost("/project/v2", () => Results.Ok("custom"));

        _ = app.UseEventStoreDomainService();

        GetMappedRoutes(app).Count(static route => route == "/project/v2").ShouldBe(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProcessRouter_SelectedBoundedProfileSerializesNormalAndAdmissionRejection(bool reject) {
        var calls = new List<string>();
        var processor = new RecordingWidgetProcessor(calls);
        var producer = new BoundedV1DomainResultProducer([
            new(typeof(WidgetCreated), "bounded-normal", "json", 2, (_, stream, token) => {
                calls.Add("serialize-normal");
                return stream.WriteAsync("{}"u8.ToArray(), token).AsTask();
            }),
            new(typeof(WidgetRejected), "bounded-rejection", "json", 2, (_, stream, token) => {
                calls.Add("serialize-rejection");
                return stream.WriteAsync("{}"u8.ToArray(), token).AsTask();
            }),
        ]);
        using ServiceProvider provider = BuildAdmissionTestProvider(processor, services => {
            services.AddSingleton(producer);
            if (reject) { services.AddEventStoreDomainAdmissionStage(_ => new RecordingAdmissionStage("gate", calls, false)); }
        });
        DomainServiceWireResult result = await DomainServiceRequestRouter.ProcessAsync(provider, CreateProcessRequest());
        result.IsRejection.ShouldBe(reject);
        result.Events.Single().EventTypeName.ShouldBe(reject ? "bounded-rejection" : "bounded-normal");
        calls.ShouldBe(reject ? ["gate", "serialize-rejection"] : ["processor", "serialize-normal"]);
        processor.InvocationCount.ShouldBe(reject ? 0 : 1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProcessRouter_PublicDomainProfileSelectsBoundedNormalAndRejection(bool reject) {
        var calls = new List<string>();
        using ServiceProvider provider = BuildAdmissionTestProvider(new RecordingWidgetProcessor(calls), services => {
            services.AddEventStoreBoundedV1DomainSerialization("widget", profile => profile
                .Add<WidgetCreated>("registered-normal", "json", 2, (_, stream, token) => {
                    calls.Add("serialize-normal");
                    return stream.WriteAsync("{}"u8.ToArray(), token).AsTask();
                })
                .Add<WidgetRejected>("registered-rejection", "json", 2, (_, stream, token) => {
                    calls.Add("serialize-rejection");
                    return stream.WriteAsync("{}"u8.ToArray(), token).AsTask();
                }));
            if (reject) { services.AddEventStoreDomainAdmissionStage(_ => new RecordingAdmissionStage("gate", calls, false)); }
        });
        DomainServiceWireResult result = await DomainServiceRequestRouter.ProcessAsync(provider, CreateProcessRequest());
        result.Events.Single().EventTypeName.ShouldBe(reject ? "registered-rejection" : "registered-normal");
        calls.ShouldBe(reject ? ["gate", "serialize-rejection"] : ["processor", "serialize-normal"]);
    }

    [Fact]
    public void BoundedV1DomainRegistration_ScopesAliasesAndRejectsDuplicateDomainOrInvalidBound() {
        var services = new ServiceCollection();
        services.AddEventStoreBoundedV1DomainSerialization("widget", profile => profile
            .Add<WidgetCreated>("same-alias", "json", 2, (_, _, _) => Task.CompletedTask));
        services.AddEventStoreBoundedV1DomainSerialization("gadget", profile => profile
            .Add<WidgetCreated>("same-alias", "json", 2, (_, _, _) => Task.CompletedTask));
        Should.Throw<ArgumentException>(() => services.AddEventStoreBoundedV1DomainSerialization("widget", _ => { }));
        Should.Throw<ArgumentException>(() => services.AddEventStoreBoundedV1DomainSerialization("other", profile => profile
            .Add<WidgetCreated>("too-large", "json", 1024 * 1024 + 1, (_, _, _) => Task.CompletedTask)));
        using ServiceProvider provider = services.BuildServiceProvider();
        DomainServiceRequestRouter.HasBoundedV1Producer(provider, "widget").ShouldBeTrue();
        DomainServiceRequestRouter.HasBoundedV1Producer(provider, "gadget").ShouldBeTrue();
        DomainServiceRequestRouter.HasBoundedV1Producer(provider, "other").ShouldBeFalse();
    }

    [Fact]
    public async Task ProcessEndpoint_SelectedProfileUsesWindowRendererInsteadOfFrameworkWholeSerialization() {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        _ = builder.AddEventStoreDomainService();
        builder.Services.AddEventStoreBoundedV1DomainSerialization("widget", profile => profile
            .Add<WidgetCreated>("bounded-é", "json", 2,
                (_, stream, token) => stream.WriteAsync("{}"u8.ToArray(), token).AsTask()));
        await using WebApplication app = builder.Build();
        _ = app.UseEventStoreDomainService();
        (int status, string body) = await InvokeEndpointAsync(app, "/process", CreateProcessRequest());
        status.ShouldBe(StatusCodes.Status200OK);
        // The selected renderer emits literal Unicode; the framework Web encoder escapes it.
        body.ShouldContain("bounded-é");
        DomainServiceWireResult result = JsonSerializer.Deserialize<DomainServiceWireResult>(body, JsonSerializerOptions.Web)!;
        result.Events.Single().Payload.ShouldBe("{}"u8.ToArray());
    }

    private static ProjectionEventDto CreateProjectionEvent()
        => new(
            EventTypeName: "WidgetCreated",
            Payload: System.Text.Encoding.UTF8.GetBytes("{}"),
            SerializationFormat: "json",
            SequenceNumber: 1,
            Timestamp: DateTimeOffset.UnixEpoch,
            CorrelationId: "test-corr");

    private static IDomainProjectionHandler CreateDuplicateWidgetProjectionHandler() {
        IDomainProjectionHandler handler = Substitute.For<IDomainProjectionHandler>();
        handler.Domain.Returns("WIDGET");
        handler.Project(Arg.Any<ProjectionRequest>()).Returns(
            new ProjectionResponse("duplicate-widget", JsonSerializer.SerializeToElement(new { duplicate = true })));
        return handler;
    }

    private static IAsyncDomainProjectionHandler CreateNamedProjectionHandler(string domain, string projectionType) {
        IAsyncDomainProjectionHandler handler = Substitute.For<IAsyncDomainProjectionHandler>();
        handler.OrdinaryDeliveryEnabled.Returns(true);
        handler.Domain.Returns(domain);
        handler.ProjectionType.Returns(projectionType);
        return handler;
    }

    private static void AssertRouteSupports(IEndpointRouteBuilder endpoints, string route, string httpMethod)
        => GetRouteEndpoints(endpoints)
            .ShouldContain(
                endpoint => string.Equals(endpoint.RoutePattern.RawText, route, StringComparison.OrdinalIgnoreCase)
                         && EndpointSupportsHttpMethod(endpoint, httpMethod),
                $"Expected route '{route}' to support HTTP {httpMethod}.");

    private static bool EndpointSupportsHttpMethod(RouteEndpoint endpoint, string httpMethod) {
        IHttpMethodMetadata? metadata = endpoint.Metadata.GetMetadata<IHttpMethodMetadata>();
        return metadata is null
            || metadata.HttpMethods.Contains(httpMethod, StringComparer.OrdinalIgnoreCase);
    }

    private static RouteEndpoint[] GetRouteEndpoints(IEndpointRouteBuilder endpoints)
        => endpoints.DataSources
            .SelectMany(static source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .ToArray();

    private static string[] GetMappedRoutes(IEndpointRouteBuilder endpoints)
        => GetRouteEndpoints(endpoints)
            .Select(static endpoint => endpoint.RoutePattern.RawText)
            .Where(static route => route is not null)
            .Select(static route => route!)
            .OrderBy(static route => route, StringComparer.Ordinal)
            .ToArray();

    private static async Task<(int StatusCode, string Body)> InvokeEndpointAsync<TRequest>(
        WebApplication app,
        string route,
        TRequest request,
        CancellationToken cancellationToken = default) {
        RouteEndpoint endpoint = GetRouteEndpoints(app).Single(candidate =>
            string.Equals(candidate.RoutePattern.RawText, route, StringComparison.Ordinal));
        byte[] body = JsonSerializer.SerializeToUtf8Bytes(request, JsonSerializerOptions.Web);
        var context = new DefaultHttpContext {
            RequestServices = new DomainRouteCatalogIsolatedServices(app.Services),
        };
        context.RequestAborted = cancellationToken;
        IHttpRequestBodyDetectionFeature bodyDetection = Substitute.For<IHttpRequestBodyDetectionFeature>();
        bodyDetection.CanHaveBody.Returns(true);
        context.Features.Set(bodyDetection);
        context.Request.Method = HttpMethods.Post;
        context.Request.ContentType = "application/json";
        context.Request.ContentLength = body.Length;
        context.Request.Body = new MemoryStream(body);
        context.Response.Body = new MemoryStream();

        await endpoint.RequestDelegate!(context);

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, Encoding.UTF8);
        string responseBody = await reader.ReadToEndAsync().ConfigureAwait(false);
        return (context.Response.StatusCode, responseBody);
    }

    private static DomainServiceRequest CreateProcessRequest()
        => new(
            new CommandEnvelope(
                MessageId: UniqueIdHelper.GenerateSortableUniqueStringId(),
                TenantId: "test-tenant",
                Domain: "widget",
                AggregateId: "widget-1",
                CommandType: nameof(CreateWidget),
                Payload: JsonSerializer.SerializeToUtf8Bytes(new CreateWidget()),
                CorrelationId: "corr-1",
                CausationId: null,
                UserId: "test-user",
                Extensions: null),
            CurrentState: null);

    private static DomainServiceRequest CreateGadgetProcessRequest()
        => new(
            new CommandEnvelope(
                MessageId: UniqueIdHelper.GenerateSortableUniqueStringId(),
                TenantId: "test-tenant",
                Domain: "gadget",
                AggregateId: "gadget-1",
                CommandType: nameof(CreateGadget),
                Payload: JsonSerializer.SerializeToUtf8Bytes(new CreateGadget()),
                CorrelationId: "corr-1",
                CausationId: null,
                UserId: "test-user",
                Extensions: null),
            CurrentState: null);

    private static ServiceProvider BuildAdmissionTestProvider(
        IDomainProcessor processor,
        Action<IServiceCollection>? configureServices = null) {
        var services = new ServiceCollection();
        _ = services.AddKeyedSingleton("widget", processor);
        configureServices?.Invoke(services);
        return services.BuildServiceProvider();
    }
}
