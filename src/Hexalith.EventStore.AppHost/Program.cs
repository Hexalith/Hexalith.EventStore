using CommunityToolkit.Aspire.Hosting.Dapr;

using Hexalith.EventStore.AppHost;
using Hexalith.EventStore.Aspire;

PrerequisiteValidator.Validate();

IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

// The local DAPR runtime can expose placement/scheduler on either the containerized host ports
// (6050/6060) or the native/slim ports (50005/50006). Resolve the actual ports up front and pass
// them to every Aspire-managed sidecar so actor routing does not depend on daprd's default guess.
(string? daprPlacementHostAddress, string? daprSchedulerHostAddress) = AspireDaprLocalServiceEndpoints.Resolve(
    builder.Configuration[AspireDaprLocalServiceEndpoints.PlacementHostAddressKey],
    builder.Configuration[AspireDaprLocalServiceEndpoints.SchedulerHostAddressKey]);

// Resolve DAPR access control configuration paths (D4, FR34).
// Each receiving service now has its own Configuration CRD so policies apply only
// to the intended inbound surface instead of every sidecar sharing one config file.
string eventStoreAccessControlConfigPath = ResolveDaprConfigPath("accesscontrol.yaml");
string adminServerAccessControlConfigPath = ResolveDaprConfigPath("accesscontrol.eventstore-admin.yaml");
string sampleAccessControlConfigPath = ResolveDaprConfigPath("accesscontrol.sample.yaml");
#if HEXALITH_TENANTS_SOURCE
string tenantsAccessControlConfigPath = ResolveDaprConfigPath("accesscontrol.tenants.yaml");
#endif
string emptyDaprResourcesPath = ResolveEmptyDaprResourcesPath();
string stateStoreComponentPath = ResolveDaprConfigPath("statestore.yaml");
string isolatedStateStoreComponentPath = ResolveIsolatedDaprComponentPath(stateStoreComponentPath);

// Resolve the DAPR resiliency YAML path so the Admin.Server can render /dapr/resiliency
// without requiring an appsettings.json edit. The AppHost is the only component that knows
// the absolute on-disk location of the DAPR resources directory, so it owns this path and
// injects it as an env var on the Admin.Server resource.
string resiliencyConfigPath = ResolveDaprConfigPath("resiliency.yaml");

// Add EventStore topology using the convenience extension
// launchSettings.json specifies port 8080 to match DAPR AppPort configuration.
IResourceBuilder<ProjectResource> eventStore = builder.AddProject<Projects.Hexalith_EventStore>("eventstore")
    // Trust internal DAPR service-invocation from domain services — the sidecar enforces ACL
    // and tags the request with `dapr-caller-app-id`, which the DaprInternal auth scheme
    // validates against this allow-list. The `tenants` service needs this so the bootstrap
    // hosted service can submit `BootstrapGlobalAdmin` to `/api/v1/commands` without carrying
    // a user JWT — domain services are behind the EventStore auth boundary by design.
    .WithEnvironment("Authentication__DaprInternal__AllowedCallers__0", "tenants");
IResourceBuilder<ProjectResource> adminServer = builder.AddProject<Projects.Hexalith_EventStore_Admin_Server_Host>("eventstore-admin");
IResourceBuilder<ProjectResource> adminUI = builder.AddProject<Projects.Hexalith_EventStore_Admin_UI>("eventstore-admin-ui");
HexalithEventStoreResources eventStoreResources = builder.AddHexalithEventStore(
    eventStore,
    adminServer,
    adminUI,
    eventStoreAccessControlConfigPath,
    adminServerAccessControlConfigPath,
    resiliencyConfigPath,
    stateStoreComponentPath: isolatedStateStoreComponentPath,
    daprPlacementHostAddress: daprPlacementHostAddress,
    daprSchedulerHostAddress: daprSchedulerHostAddress);

ForwardEventStoreEnvironment("EventStore:Publisher:TestPublishFaultFilePath", "EventStore__Publisher__TestPublishFaultFilePath");
ForwardEventStoreEnvironment("EventStore:Publisher:TestPublishFaultCorrelationIdPrefix", "EventStore__Publisher__TestPublishFaultCorrelationIdPrefix");
ForwardEventStoreEnvironment("EventStore:Drain:InitialDrainDelay", "EventStore__Drain__InitialDrainDelay");
ForwardEventStoreEnvironment("EventStore:Drain:DrainPeriod", "EventStore__Drain__DrainPeriod");
ForwardEventStoreEnvironment("EventStore:Drain:MaxDrainPeriod", "EventStore__Drain__MaxDrainPeriod");
ForwardEventStoreEnvironment("EventStore:Actors:AggregateActorTypeName", "EventStore__Actors__AggregateActorTypeName");

// Keycloak identity provider for E2E security testing (D11, Story 5.1 Task 8).
// Enabled by default for local development with real OIDC token testing.
// Set EnableKeycloak=false in environment or appsettings to run without Keycloak
// (falls back to symmetric key auth via Authentication:JwtBearer:SigningKey).
HexalithEventStoreSecurityResources? security = builder.AddHexalithEventStoreSecurity();
if (security is not null) {
    _ = eventStore.WithJwtBearerSecurity(security);
}

// Add Tenants domain service with DAPR sidecar via the platform domain-module extension (A4).
// Tenants shares the same state store and pub/sub as EventStore (no isolated resources path).
// The external Tenants host project is included only in source-debug mode. Release/package mode
// must not compile cross-repo Hexalith source just because a submodule is checked out.
#if HEXALITH_TENANTS_SOURCE
IResourceBuilder<ProjectResource> tenants = builder.AddProject<Projects.Hexalith_Tenants>("tenants")
    .AddEventStoreDomainModule(
        eventStoreResources,
        "tenants",
        tenantsAccessControlConfigPath,
        daprPlacementHostAddress: daprPlacementHostAddress,
        daprSchedulerHostAddress: daprSchedulerHostAddress)
    .WithEnvironment("Tenants__BootstrapGlobalAdminUserId", "admin-user");
#endif

// Add sample domain service with DAPR sidecar via the platform domain-module extension (A4).
// Passing the empty resources path makes the sample fully isolated: its sidecar does NOT reference
// Redis, StateStore, or PubSub. Domain services have zero infrastructure access (D4, AC #13) —
// not loading these component definitions is stronger isolation than scoping alone.
IResourceBuilder<ProjectResource> sample = builder.AddProject<Projects.Hexalith_EventStore_Sample>("sample")
    .AddEventStoreDomainModule(
        eventStoreResources,
        "sample",
        sampleAccessControlConfigPath,
        isolatedDaprResourcesPath: emptyDaprResourcesPath,
        daprPlacementHostAddress: daprPlacementHostAddress,
        daprSchedulerHostAddress: daprSchedulerHostAddress);

// Add Blazor UI sample — invokes EventStore via DAPR service invocation (mirrors Admin.UI D13).
// Enables SignalR on EventStore so the hub is active when the Blazor UI is running.
_ = eventStore.WithEnvironment("EventStore__SignalR__Enabled", "true");
EndpointReference eventStoreHttps = eventStore.GetEndpoint("https");
EndpointReference adminServerHttps = adminServer.GetEndpoint("https");

// The BlazorUI sidecar references no state store / pub/sub component — service invocation
// only, so it has zero direct infrastructure access (same isolation rationale as the sample
// and admin-ui sidecars). DaprAppIdHandler tags outbound query calls with
// `dapr-app-id: eventstore`. WaitFor(eventStore) is retained so the UI starts after its target.
IResourceBuilder<ProjectResource> blazorUi = builder.AddProject<Projects.Hexalith_EventStore_Sample_BlazorUI>("sample-blazor-ui")
    .WithReference(eventStore)
    .WaitFor(eventStore)
    .WithExternalHttpEndpoints()
    .WithDaprSidecar(sidecar => sidecar
        .WithOptions(new DaprSidecarOptions {
            AppId = "sample-blazor-ui",
            PlacementHostAddress = daprPlacementHostAddress,
            SchedulerHostAddress = daprSchedulerHostAddress,
        }))
    // SignalR HubConnectionBuilder bypasses Aspire service discovery (it doesn't use HttpClientFactory),
    // so we must pass the resolved eventstore endpoint URL explicitly.
    .WithEnvironment("EventStore__SignalR__HubUrl", ReferenceExpression.Create($"{eventStoreHttps}/hubs/projection-changes"));

// Add the external-facing REST API sample host. It hosts the source-generated typed controllers for
// third-party callers and reaches EventStore via DAPR service invocation (dapr-app-id: eventstore),
// forwarding the caller's validated bearer. Like the Blazor UI / admin-ui sidecars it references no
// state store or pub/sub component — service invocation only, so it has zero direct infrastructure access.
IResourceBuilder<ProjectResource> sampleApi = builder.AddProject<Projects.Hexalith_EventStore_Sample_Api>("sample-api")
    .WithReference(eventStore)
    .WaitFor(eventStore)
    .WithExternalHttpEndpoints()
    .WithDaprSidecar(sidecar => sidecar
        .WithOptions(new DaprSidecarOptions {
            AppId = "sample-api",
            PlacementHostAddress = daprPlacementHostAddress,
            SchedulerHostAddress = daprSchedulerHostAddress,
        }));

if (security is not null) {
#if HEXALITH_TENANTS_SOURCE
    _ = tenants.WithJwtBearerSecurity(security);
#endif

    _ = adminServer.WithJwtBearerSecurity(security);

    _ = blazorUi.WithEventStoreClientCredentials(security);

    // sample-api validates inbound callers against the same realm; WithEventStoreClientCredentials wires
    // EventStore:Authentication:Authority so JWT validation uses OIDC discovery (Keycloak). With
    // EnableKeycloak=false it falls back to the symmetric signing key from appsettings.Development.json.
    _ = sampleApi.WithEventStoreClientCredentials(security);

    _ = adminUI
        .WithEventStoreClientCredentials(security)
        .WithEnvironment("EventStore__AdminServer__SwaggerUrl", ReferenceExpression.Create($"{adminServerHttps}/swagger/index.html"));
}
else {
    _ = adminUI.WithEnvironment("EventStore__AdminServer__SwaggerUrl", ReferenceExpression.Create($"{adminServerHttps}/swagger/index.html"));
}

// Publisher environments (only activate during `aspire publish`).
ConfigurePublishEnvironment(builder);

// EnablePubSubTestSubscriber accepts any value parseable as bool (true/false/1/0, case-insensitive,
// trimmed). The eventstore-test-subscriber app-id grants in DaprComponents/pubsub.yaml are documented
// dev-only -- production deployment overlays (k8s/docker/aca via aspire publish) must strip them.
bool testSubscriberEnabled = bool.TryParse(builder.Configuration["EnablePubSubTestSubscriber"]?.Trim(), out bool parsed) && parsed;
string testSubscriberTopic = builder.Configuration["EVENTSTORE_TEST_SUBSCRIBER_TOPIC"]?.Trim() ?? "tenant-a.counter.events";
string testSubscriberAuthSecret = builder.Configuration["EVENTSTORE_TEST_SUBSCRIBER_AUTH_SECRET"]?.Trim() ?? Guid.NewGuid().ToString("N");
if (testSubscriberEnabled) {
    IResourceBuilder<ProjectResource> testSubscriber = builder.AddProject<Projects.Hexalith_EventStore_TestSubscriber>("eventstore-test-subscriber")
        .WithDaprSidecar(sidecar => sidecar
            .WithOptions(new DaprSidecarOptions {
                AppId = "eventstore-test-subscriber",
                PlacementHostAddress = daprPlacementHostAddress,
                SchedulerHostAddress = daprSchedulerHostAddress,
            })
            .WithReference(eventStoreResources.PubSub))
        .WaitFor(eventStore);

    _ = testSubscriber
        .WithEnvironment("EVENTSTORE_TEST_SUBSCRIBER_TOPIC", testSubscriberTopic)
        .WithEnvironment("EVENTSTORE_TEST_SUBSCRIBER_AUTH_SECRET", testSubscriberAuthSecret);
}

await builder.Build().RunAsync().ConfigureAwait(false);

static string ResolveDaprConfigPath(string fileName) {
    string configPath = Path.Combine(Directory.GetCurrentDirectory(), "DaprComponents", fileName);
    if (!File.Exists(configPath)) {
        configPath = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "DaprComponents", fileName));
    }

    if (!File.Exists(configPath)) {
        throw new FileNotFoundException(
            "DAPR access control configuration not found. "
            + $"Ensure {fileName} exists in the DaprComponents directory (D4, FR34).",
            configPath);
    }

    return configPath;
}

// Aspire requires exactly one compute environment per resource at publish time.
// Set PUBLISH_TARGET to select the target publisher:
//   PUBLISH_TARGET=docker  -> Docker Compose (docker-compose.yaml + .env)
//   PUBLISH_TARGET=k8s     -> Kubernetes (Helm charts / K8s YAML manifests)
//   PUBLISH_TARGET=aca     -> Azure Container Apps (Bicep modules)
// Example: PUBLISH_TARGET=docker aspire publish -o ./publish-output/docker
static void ConfigurePublishEnvironment(IDistributedApplicationBuilder builder) {
    string? publishTarget = builder.Configuration["PUBLISH_TARGET"];
    if (string.Equals(publishTarget, "docker", StringComparison.OrdinalIgnoreCase)) {
        _ = builder.AddDockerComposeEnvironment("docker");
    }
    else if (string.Equals(publishTarget, "k8s", StringComparison.OrdinalIgnoreCase)) {
        _ = builder.AddKubernetesEnvironment("k8s");
    }
    else if (string.Equals(publishTarget, "aca", StringComparison.OrdinalIgnoreCase)) {
        _ = builder.AddAzureContainerAppEnvironment("aca");
    }
}

static string ResolveEmptyDaprResourcesPath() {
    string path = Path.Combine(Path.GetTempPath(), "hexalith-eventstore-empty-dapr-resources");
    _ = Directory.CreateDirectory(path);
    return path;
}

static string ResolveIsolatedDaprComponentPath(string sourcePath) {
    string sourceFullPath = Path.GetFullPath(sourcePath);
    if (!File.Exists(sourceFullPath)) {
        throw new FileNotFoundException(
            "DAPR state-store component source not found.",
            sourceFullPath);
    }

    string targetDirectory = Path.Combine(Path.GetTempPath(), "hexalith-eventstore-dapr-components", "statestore");
    _ = Directory.CreateDirectory(targetDirectory);
    foreach (string existingYaml in Directory.GetFiles(targetDirectory, "*.yaml")) {
        File.Delete(existingYaml);
    }

    string targetPath = Path.Combine(targetDirectory, Path.GetFileName(sourceFullPath));
    File.Copy(sourceFullPath, targetPath, overwrite: true);
    return targetPath;
}

void ForwardEventStoreEnvironment(string configurationKey, string environmentKey) {
    string? value = builder.Configuration[configurationKey];
    if (!string.IsNullOrWhiteSpace(value)) {
        _ = eventStore.WithEnvironment(environmentKey, value);
    }
}
