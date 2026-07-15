using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text;
using System.Text.Json;

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

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.DomainService.Tests;

/// <summary>
/// Tier 1 coverage of the domain-service SDK host extensions and the moved router/metadata helpers.
/// </summary>
public sealed class EventStoreDomainServiceExtensionsTests {
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
    /// Proves request cancellation reaches the admission stage and prevents processor dispatch.
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
        calls.ShouldBe(["auth"]);
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
    /// Proves the canonical domain-service endpoint set stays unchanged when the admission hook is added.
    /// </summary>
    [Fact]
    public void MapEventStoreDomainService_MapsCanonicalEndpointsUnchanged() {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        _ = builder.Services.AddSingleton(new DiscoveryResult([], []));
        WebApplication app = builder.Build();

        _ = app.MapEventStoreDomainService();

        string[] routes = GetMappedRoutes(app);

        routes.ShouldBe([
            "/",
            "/admin/operational-index-metadata",
            "/process",
            "/project",
            "/project/rebuild/v1",
            "/project/v2",
            "/query",
            "/replay-state"]);
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
            [new ProjectionDispatchRoute("widget", "widget-detail")]);
        var metadataRequest = new AdminOperationalIndexMetadata.Request(["widget"]) {
            AppId = "sample",
            ServiceVersion = "v1",
        };

        (int metadataStatus, string metadataBody) = await InvokeEndpointAsync(
            app,
            "/admin/operational-index-metadata",
            metadataRequest);

        metadataStatus.ShouldBe(StatusCodes.Status200OK, metadataBody);
        app.Services.GetRequiredService<DomainProjectionCatalogRegistry>().Contains(fingerprint).ShouldBeTrue();

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
        TRequest request) {
        RouteEndpoint endpoint = GetRouteEndpoints(app).Single(candidate =>
            string.Equals(candidate.RoutePattern.RawText, route, StringComparison.Ordinal));
        byte[] body = JsonSerializer.SerializeToUtf8Bytes(request, JsonSerializerOptions.Web);
        var context = new DefaultHttpContext {
            RequestServices = app.Services,
        };
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
