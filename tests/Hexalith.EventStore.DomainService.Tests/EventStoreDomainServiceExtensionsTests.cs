using System.Diagnostics;
using System.Text.Json;

using Hexalith.EventStore.Client.Discovery;
using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Client.Registration;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.DomainService.Tests.Fixtures;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

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
                MessageId: Guid.NewGuid().ToString(),
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
            "/query",
            "/replay-state"]);
    }

    private static ProjectionEventDto CreateProjectionEvent()
        => new(
            EventTypeName: "WidgetCreated",
            Payload: System.Text.Encoding.UTF8.GetBytes("{}"),
            SerializationFormat: "json",
            SequenceNumber: 1,
            Timestamp: DateTimeOffset.UnixEpoch,
            CorrelationId: "test-corr");

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

    private static DomainServiceRequest CreateProcessRequest()
        => new(
            new CommandEnvelope(
                MessageId: Guid.NewGuid().ToString(),
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

    private static ServiceProvider BuildAdmissionTestProvider(
        IDomainProcessor processor,
        Action<IServiceCollection>? configureServices = null) {
        var services = new ServiceCollection();
        _ = services.AddKeyedSingleton("widget", processor);
        configureServices?.Invoke(services);
        return services.BuildServiceProvider();
    }
}
