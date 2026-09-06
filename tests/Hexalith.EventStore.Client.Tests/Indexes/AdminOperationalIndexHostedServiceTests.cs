using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;

using Dapr.Client;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Indexes;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Projections;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Indexes;

public class AdminOperationalIndexHostedServiceTests {
    [Fact]
    public async Task StartAsync_CompleteNamedMetadata_PublishesExactLoadedCatalog() {
        ProjectionDispatchRoute[] routes = [new("counter", "counter-detail")];
        string fingerprint = ProjectionRouteCatalogFingerprint.Compute("sample", "v1", routes);
        var response = new AdminOperationalIndexMetadataResponse([
            new AdminOperationalIndexDomainMetadata(
                "counter",
                [],
                [],
                [],
                ["CounterAggregate"],
                []) {
                NamedProjectionTypes = ["counter-detail"],
            },
        ]) {
            DispatchVersion = ProjectionDispatchProtocol.Version,
            DispatchCapability = ProjectionDispatchProtocol.Capability,
            AppId = "sample",
            ServiceVersion = "v1",
            CatalogFingerprint = fingerprint,
        };
        DaprClient daprClient = CreateDaprClient();
        IHttpClientFactory httpClientFactory = CreateHttpClientFactory(() => new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new StringContent(JsonSerializer.Serialize(response, JsonSerializerOptions.Web), Encoding.UTF8, "application/json"),
        });
        INamedProjectionRouteCatalog routeCatalog = Substitute.For<INamedProjectionRouteCatalog>();
        AdminOperationalIndexHostedService hostedService = CreateHostedService(daprClient, httpClientFactory, routeCatalog);

        await hostedService.StartAsync(CancellationToken.None);

        routeCatalog.Received(1).Replace(Arg.Is<NamedProjectionRouteCatalogSnapshot>(snapshot =>
            snapshot.Entries.Count == 1
            && snapshot.Entries[0].AppId == "sample"
            && snapshot.Entries[0].ServiceVersion == "v1"
            && snapshot.Entries[0].Domain == "counter"
            && snapshot.Entries[0].CatalogFingerprint == fingerprint
            && snapshot.Entries[0].ProjectionTypes.SequenceEqual(new[] { "counter-detail" }, StringComparer.Ordinal)));
        _ = daprClient.Received(1).CreateInvokeMethodRequest(
            HttpMethod.Post,
            "sample",
            "admin/operational-index-metadata",
            Arg.Any<IReadOnlyCollection<KeyValuePair<string, string>>>(),
            Arg.Is<AdminOperationalIndexMetadataRequest>(request =>
                request.AppId == "sample"
                && request.ServiceVersion == "v1"
                && request.Domains.SequenceEqual(new[] { "counter" }, StringComparer.Ordinal)));
    }

    [Fact]
    public async Task StartAsync_MetadataFailure_PreservesExistingCatalogAndIndexes() {
        DaprClient daprClient = CreateDaprClient();
        IHttpClientFactory httpClientFactory = CreateHttpClientFactory(
            () => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        INamedProjectionRouteCatalog routeCatalog = Substitute.For<INamedProjectionRouteCatalog>();
        AdminOperationalIndexHostedService hostedService = CreateHostedService(daprClient, httpClientFactory, routeCatalog);

        await hostedService.StartAsync(CancellationToken.None);

        routeCatalog.DidNotReceiveWithAnyArgs().Replace(default!);
        daprClient.ReceivedCalls().ShouldNotContain(static call => call.GetMethodInfo().Name == nameof(DaprClient.SaveStateAsync));
    }

    [Fact]
    public async Task StartAsync_MalformedSuccessfulResponsePreservesExistingCatalogAndIndexes() {
        DaprClient daprClient = CreateDaprClient();
        IHttpClientFactory httpClientFactory = CreateHttpClientFactory(
            () => new HttpResponseMessage(HttpStatusCode.OK) {
                Content = new StringContent("null", Encoding.UTF8, "application/json"),
            });
        INamedProjectionRouteCatalog routeCatalog = Substitute.For<INamedProjectionRouteCatalog>();
        AdminOperationalIndexHostedService hostedService = CreateHostedService(daprClient, httpClientFactory, routeCatalog);

        await hostedService.StartAsync(CancellationToken.None);
        await hostedService.StopAsync(CancellationToken.None);

        routeCatalog.DidNotReceiveWithAnyArgs().Replace(default!);
        daprClient.ReceivedCalls().ShouldNotContain(static call => call.GetMethodInfo().Name == nameof(DaprClient.SaveStateAsync));
    }

    [Fact]
    public async Task StartAsync_PartialMetadataFailure_WritesRecoveredHandlerQueryTypesWithoutProjectionIndexes() {
        AdminOperationalIndexMetadataResponse tenantsResponse = CreateTenantsMetadataResponse(["get-tenant", "list-tenants"]);
        DaprClient daprClient = CreateDaprClient();
        IHttpClientFactory httpClientFactory = CreateHttpClientFactory(request => {
            if (IsAppId(request, "tenants")) {
                return JsonResponse(tenantsResponse);
            }

            return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        });
        INamedProjectionRouteCatalog routeCatalog = Substitute.For<INamedProjectionRouteCatalog>();
        AdminOperationalIndexHostedService hostedService = CreateHostedService(
            daprClient,
            httpClientFactory,
            routeCatalog,
            new DomainServiceRegistration("sample", "process", "*", "counter", "v1"),
            new DomainServiceRegistration("tenants", "process", "*", "tenants", "v1"));

        await hostedService.StartAsync(CancellationToken.None);
        await hostedService.StopAsync(CancellationToken.None);

        routeCatalog.DidNotReceiveWithAnyArgs().Replace(default!);
        IReadOnlyList<(string Key, object? Value)> saved = GetSavedStates(daprClient);
        saved.Select(static item => item.Key).ShouldBe(["admin:query-types:tenants"]);
        ((IEnumerable<string>)saved[0].Value!).ShouldBe(["get-tenant", "list-tenants"]);
    }

    [Fact]
    public async Task StartAsync_PartialMetadataFailure_DoesNotWriteEmptyQueryTypeCatalog() {
        AdminOperationalIndexMetadataResponse tenantsResponse = CreateTenantsMetadataResponse([]);
        DaprClient daprClient = CreateDaprClient();
        IHttpClientFactory httpClientFactory = CreateHttpClientFactory(request => {
            if (IsAppId(request, "tenants")) {
                return JsonResponse(tenantsResponse);
            }

            return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        });
        INamedProjectionRouteCatalog routeCatalog = Substitute.For<INamedProjectionRouteCatalog>();
        AdminOperationalIndexHostedService hostedService = CreateHostedService(
            daprClient,
            httpClientFactory,
            routeCatalog,
            new DomainServiceRegistration("sample", "process", "*", "counter", "v1"),
            new DomainServiceRegistration("tenants", "process", "*", "tenants", "v1"));

        await hostedService.StartAsync(CancellationToken.None);
        await hostedService.StopAsync(CancellationToken.None);

        routeCatalog.DidNotReceiveWithAnyArgs().Replace(default!);
        daprClient.ReceivedCalls().ShouldNotContain(static call => call.GetMethodInfo().Name == nameof(DaprClient.SaveStateAsync));
    }

    [Fact]
    public async Task RefreshAsync_RecoveredBinding_WritesHandlerQueryTypesAfterStartupFailure() {
        int tenantsRequests = 0;
        AdminOperationalIndexMetadataResponse tenantsResponse = CreateTenantsMetadataResponse(["get-tenant"]);
        DaprClient daprClient = CreateDaprClient();
        IHttpClientFactory httpClientFactory = CreateHttpClientFactory(request => {
            if (!IsAppId(request, "tenants")) {
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            }

            tenantsRequests++;
            return tenantsRequests == 1
                ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                : JsonResponse(tenantsResponse);
        });
        INamedProjectionRouteCatalog routeCatalog = Substitute.For<INamedProjectionRouteCatalog>();
        var tenantsRegistration = new DomainServiceRegistration("tenants", "process", "*", "tenants", "v1");
        AdminOperationalIndexHostedService hostedService = CreateHostedService(
            daprClient,
            httpClientFactory,
            routeCatalog,
            new DomainServiceRegistration("sample", "process", "*", "counter", "v1"),
            tenantsRegistration);

        await hostedService.StartAsync(CancellationToken.None);
        GetSavedStates(daprClient).ShouldBeEmpty();

        bool namedRoutesPublished = await hostedService.RefreshAsync(tenantsRegistration);
        await hostedService.StopAsync(CancellationToken.None);

        namedRoutesPublished.ShouldBeFalse();
        IReadOnlyList<(string Key, object? Value)> saved = GetSavedStates(daprClient);
        saved.Select(static item => item.Key).ShouldBe(["admin:query-types:tenants"]);
        ((IEnumerable<string>)saved[0].Value!).ShouldBe(["get-tenant"]);
    }

    [Fact]
    public void BuildSnapshot_RejectsProjectionActorKeysAndBuildsCatalogPayloads() {
        var metadata = new AdminOperationalIndexDomainMetadata(
            "counter",
            ["Hexalith.EventStore.Sample.Counter.Events.CounterIncremented"],
            [],
            ["Hexalith.EventStore.Sample.Counter.Commands.IncrementCounter"],
            ["Hexalith.EventStore.Sample.Counter.CounterAggregate"],
            ["counter", "eventstore||ProjectionActor||counter||projection-state"]);
        var wildcardRegistration = new DomainServiceRegistration("sample", "process", "*", "counter", "v1");
        var tenantRegistration = new DomainServiceRegistration("sample", "process", "tenant-a", "orders", "v1");

        AdminOperationalIndexSnapshot snapshot = AdminOperationalIndexHostedService.BuildSnapshot(
            [metadata],
            [wildcardRegistration, tenantRegistration],
            new ProjectionOptions());

        snapshot.Projections.Count.ShouldBe(1);
        snapshot.Projections[0].Name.ShouldBe("counter");
        snapshot.Projections[0].TenantId.ShouldBe("all");
        snapshot.TenantProjections["tenant-a"].ShouldContain(p => p.Name == "counter" && p.TenantId == "tenant-a");
        snapshot.Projections.ShouldNotContain(p => p.Name == "orders");
        snapshot.EventTypes.Single().SchemaVersion.ShouldBe(1);
        snapshot.CommandTypes.Single().TargetAggregateType.ShouldBe("Hexalith.EventStore.Sample.Counter.CounterAggregate");
        snapshot.AggregateTypes.Single().HasProjections.ShouldBeTrue();
    }

    [Fact]
    public void BuildSnapshot_MaterializesHandlerQueryTypesByDomain() {
        var metadata = new AdminOperationalIndexDomainMetadata(
            "tenants",
            ["Hexalith.Tenants.Contracts.Events.TenantCreated"],
            [],
            ["Hexalith.Tenants.Contracts.Commands.CreateTenant"],
            ["Hexalith.Tenants.TenantAggregate"],
            ["tenants"],
            ["list-tenants", "get-tenant"]);
        var registration = new DomainServiceRegistration("tenants", "process", "*", "tenants", "v1");

        AdminOperationalIndexSnapshot snapshot = AdminOperationalIndexHostedService.BuildSnapshot(
            [metadata],
            [registration],
            new ProjectionOptions());

        _ = snapshot.QueryTypesByDomain.ShouldNotBeNull();
        // Materialized de-duplicated and ordered (get-tenant < list-tenants).
        snapshot.QueryTypesByDomain!["tenants"].ShouldBe(["get-tenant", "list-tenants"]);
    }

    [Fact]
    public void BuildSnapshot_MergesQueryTypesWithoutLosingCatalogData() {
        var first = new AdminOperationalIndexDomainMetadata(
            "tenants",
            ["Hexalith.Tenants.Contracts.Events.TenantCreated"],
            [],
            ["Hexalith.Tenants.Contracts.Commands.CreateTenant"],
            ["Hexalith.Tenants.TenantAggregate"],
            ["tenants"],
            ["get-tenant"]);
        var second = new AdminOperationalIndexDomainMetadata(
            "tenants",
            ["Hexalith.Tenants.Contracts.Events.TenantRenamed"],
            [],
            ["Hexalith.Tenants.Contracts.Commands.RenameTenant"],
            ["Hexalith.Tenants.TenantAggregate"],
            ["tenant-search"],
            ["list-tenants", "get-tenant"]);
        var registration = new DomainServiceRegistration("tenants", "process", "*", "tenants", "v1");

        AdminOperationalIndexDomainMetadata merged = MergeDomainMetadata("tenants", [first, second]);

        AdminOperationalIndexSnapshot snapshot = AdminOperationalIndexHostedService.BuildSnapshot(
            [merged],
            [registration],
            new ProjectionOptions());

        snapshot.EventTypes.Select(e => e.TypeName).ShouldBe([
            "Hexalith.Tenants.Contracts.Events.TenantCreated",
            "Hexalith.Tenants.Contracts.Events.TenantRenamed",
        ]);
        snapshot.CommandTypes.Select(c => c.TypeName).ShouldBe([
            "Hexalith.Tenants.Contracts.Commands.CreateTenant",
            "Hexalith.Tenants.Contracts.Commands.RenameTenant",
        ]);
        snapshot.AggregateTypes.ShouldContain(a => a.TypeName == "Hexalith.Tenants.TenantAggregate");
        snapshot.Projections.Select(p => p.Name).ShouldBe(["tenant-search", "tenants"]);
        _ = snapshot.QueryTypesByDomain.ShouldNotBeNull();
        snapshot.QueryTypesByDomain!["tenants"].ShouldBe(["get-tenant", "list-tenants"]);
    }

    [Fact]
    public void BuildSnapshot_DoesNotInventProjection_WhenMetadataMissing() {
        var registration = new DomainServiceRegistration("sample", "process", "tenant-a", "orders", "v1");

        AdminOperationalIndexSnapshot snapshot = AdminOperationalIndexHostedService.BuildSnapshot(
            [],
            [registration],
            new ProjectionOptions());

        snapshot.Projections.ShouldBeEmpty();
        snapshot.TenantProjections.ShouldBeEmpty();
    }

    [Fact]
    public void BuildSnapshot_PersistsExactMergedNamedProjectionTypes() {
        var first = new AdminOperationalIndexDomainMetadata(
            "counter",
            [],
            [],
            [],
            ["CounterAggregate"],
            []) {
            NamedProjectionTypes = ["counter-index"],
        };
        var second = new AdminOperationalIndexDomainMetadata(
            "counter",
            [],
            [],
            [],
            ["CounterAggregate"],
            []) {
            NamedProjectionTypes = ["counter-detail", "counter-index"],
        };
        AdminOperationalIndexDomainMetadata merged = MergeDomainMetadata("counter", [first, second]);
        var registration = new DomainServiceRegistration("sample", "process", "tenant-a", "counter", "v1");

        AdminOperationalIndexSnapshot snapshot = AdminOperationalIndexHostedService.BuildSnapshot(
            [merged],
            [registration],
            new ProjectionOptions());

        merged.NamedProjectionTypes.ShouldBe(["counter-detail", "counter-index"]);
        snapshot.Projections.Select(static projection => projection.Name)
            .ShouldBe(["counter-detail", "counter-index"]);
        snapshot.AggregateTypes.ShouldHaveSingleItem().HasProjections.ShouldBeTrue();
    }

    [Fact]
    public void BuildSnapshot_BoundMetadataDoesNotLeakProjectionRoutesAcrossApps() {
        var appOneMetadata = new AdminOperationalIndexDomainMetadata("counter", [], [], [], [], []) {
            NamedProjectionTypes = ["counter-detail"],
        };
        var appTwoMetadata = new AdminOperationalIndexDomainMetadata("counter", [], [], [], [], []) {
            NamedProjectionTypes = ["counter-index"],
        };
        var appOne = new DomainServiceRegistration("app-one", "process", "tenant-a", "counter", "v1");
        var appTwo = new DomainServiceRegistration("app-two", "process", "tenant-b", "counter", "v1");
        var bound = new Dictionary<(string AppId, string ServiceVersion, string Domain), AdminOperationalIndexDomainMetadata> {
            [("app-one", "v1", "counter")] = appOneMetadata,
            [("app-two", "v1", "counter")] = appTwoMetadata,
        };

        AdminOperationalIndexSnapshot snapshot = AdminOperationalIndexHostedService.BuildSnapshot(
            [appOneMetadata, appTwoMetadata],
            [appOne, appTwo],
            new ProjectionOptions(),
            bound);

        snapshot.TenantProjections["tenant-a"].Select(static item => item.Name).ShouldBe(["counter-detail"]);
        snapshot.TenantProjections["tenant-b"].Select(static item => item.Name).ShouldBe(["counter-index"]);
    }

    private static AdminOperationalIndexDomainMetadata MergeDomainMetadata(
        string domain,
        IEnumerable<AdminOperationalIndexDomainMetadata> metadata) {
        MethodInfo method = typeof(AdminOperationalIndexHostedService).GetMethod(
            "MergeDomainMetadata",
            BindingFlags.Static | BindingFlags.NonPublic)!;
        return (AdminOperationalIndexDomainMetadata)method.Invoke(null, [domain, metadata])!;
    }

    private static AdminOperationalIndexHostedService CreateHostedService(
        DaprClient daprClient,
        IHttpClientFactory httpClientFactory,
        INamedProjectionRouteCatalog routeCatalog,
        params DomainServiceRegistration[] registrations) {
        DomainServiceRegistration[] effectiveRegistrations = registrations.Length == 0
            ? [new DomainServiceRegistration("sample", "process", "*", "counter", "v1")]
            : registrations;
        var domainOptions = new DomainServiceOptions();
        foreach (DomainServiceRegistration registration in effectiveRegistrations) {
            string version = string.IsNullOrWhiteSpace(registration.Version) ? "v1" : registration.Version;
            domainOptions.Registrations[$"{registration.TenantId}:{registration.Domain}:{version}:{registration.AppId}"] = registration;
        }

        return new AdminOperationalIndexHostedService(
            daprClient,
            httpClientFactory,
            Options.Create(new CommandStatusOptions()),
            Options.Create(domainOptions),
            Options.Create(new ProjectionOptions()),
            routeCatalog,
            NullLogger<AdminOperationalIndexHostedService>.Instance);
    }

    private static DaprClient CreateDaprClient() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.CreateInvokeMethodRequest(
                HttpMethod.Post,
                Arg.Any<string>(),
                "admin/operational-index-metadata",
                Arg.Any<IReadOnlyCollection<KeyValuePair<string, string>>>(),
                Arg.Any<AdminOperationalIndexMetadataRequest>())
            .Returns(callInfo => new HttpRequestMessage(
                HttpMethod.Post,
                $"http://metadata/{callInfo.ArgAt<string>(1)}"));
        return daprClient;
    }

    private static IHttpClientFactory CreateHttpClientFactory(Func<HttpResponseMessage> responseFactory)
        => CreateHttpClientFactory(_ => responseFactory());

    private static IHttpClientFactory CreateHttpClientFactory(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) {
        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        _ = factory.CreateClient(Arg.Any<string>())
            .Returns(new HttpClient(new AdminOperationalIndexHttpMessageHandler(responseFactory)));
        _ = factory.CreateClient()
            .Returns(new HttpClient(new AdminOperationalIndexHttpMessageHandler(responseFactory)));
        return factory;
    }

    private static AdminOperationalIndexMetadataResponse CreateTenantsMetadataResponse(IReadOnlyList<string> queryTypes)
        => new([
            new AdminOperationalIndexDomainMetadata(
                "tenants",
                ["Hexalith.Tenants.Contracts.Events.TenantCreated"],
                [],
                ["Hexalith.Tenants.Contracts.Commands.CreateTenant"],
                ["Hexalith.Tenants.TenantAggregate"],
                ["tenants"],
                queryTypes),
        ]);

    private static HttpResponseMessage JsonResponse(AdminOperationalIndexMetadataResponse response)
        => new(HttpStatusCode.OK) {
            Content = new StringContent(
                JsonSerializer.Serialize(response, JsonSerializerOptions.Web),
                Encoding.UTF8,
                "application/json"),
        };

    private static bool IsAppId(HttpRequestMessage request, string appId)
        => string.Equals(request.RequestUri?.AbsolutePath.Trim('/'), appId, StringComparison.Ordinal);

    private static IReadOnlyList<(string Key, object? Value)> GetSavedStates(DaprClient daprClient)
        => [.. daprClient.ReceivedCalls()
            .Where(static call => string.Equals(call.GetMethodInfo().Name, nameof(DaprClient.SaveStateAsync), StringComparison.Ordinal))
            .Select(static call => (
                Key: (string)call.GetArguments()[1]!,
                Value: call.GetArguments()[2]))];
}
