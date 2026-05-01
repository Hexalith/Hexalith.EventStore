using CommunityToolkit.Aspire.Hosting.Dapr;

using Hexalith.EventStore.AppHost;
using Hexalith.EventStore.Aspire;

const string FalseLiteral = "false";

PrerequisiteValidator.Validate();

IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

// Resolve DAPR access control configuration paths (D4, FR34).
// Each receiving service now has its own Configuration CRD so policies apply only
// to the intended inbound surface instead of every sidecar sharing one config file.
string eventStoreAccessControlConfigPath = ResolveDaprConfigPath("accesscontrol.yaml");
string adminServerAccessControlConfigPath = ResolveDaprConfigPath("accesscontrol.eventstore-admin.yaml");
string sampleAccessControlConfigPath = ResolveDaprConfigPath("accesscontrol.sample.yaml");
string tenantsAccessControlConfigPath = ResolveDaprConfigPath("accesscontrol.tenants.yaml");

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
    resiliencyConfigPath);

ForwardEventStoreEnvironment("EventStore:Publisher:TestPublishFaultFilePath", "EventStore__Publisher__TestPublishFaultFilePath");
ForwardEventStoreEnvironment("EventStore:Publisher:TestPublishFaultCorrelationIdPrefix", "EventStore__Publisher__TestPublishFaultCorrelationIdPrefix");
ForwardEventStoreEnvironment("EventStore:Drain:InitialDrainDelay", "EventStore__Drain__InitialDrainDelay");
ForwardEventStoreEnvironment("EventStore:Drain:DrainPeriod", "EventStore__Drain__DrainPeriod");
ForwardEventStoreEnvironment("EventStore:Drain:MaxDrainPeriod", "EventStore__Drain__MaxDrainPeriod");

// Keycloak identity provider for E2E security testing (D11, Story 5.1 Task 8).
// Enabled by default for local development with real OIDC token testing.
// Set EnableKeycloak=false in environment or appsettings to run without Keycloak
// (falls back to symmetric key auth via Authentication:JwtBearer:SigningKey).
IResourceBuilder<KeycloakResource>? keycloak = null;
ReferenceExpression? realmUrl = null;
if (!string.Equals(builder.Configuration["EnableKeycloak"], FalseLiteral, StringComparison.OrdinalIgnoreCase)) {
    // Realm-as-code: hexalith-realm.json auto-imported on container start.
    // Port 8180 avoids conflict with eventstore on 8080.
    keycloak = builder.AddKeycloak("keycloak", 8180)
        .WithRealmImport("./KeycloakRealms");

    // Wire Keycloak OIDC auth (D11, Story 5.1 Task 8).
    // The existing ConfigureJwtBearerOptions.cs OIDC discovery path handles everything
    // when Authentication:JwtBearer:Authority is set to the Keycloak realm URL.
    EndpointReference keycloakEndpoint = keycloak.GetEndpoint("http");
    realmUrl = ReferenceExpression.Create($"{keycloakEndpoint}/realms/hexalith");
    _ = eventStore
        .WithReference(keycloak)
        .WaitFor(keycloak)
        .WithEnvironment("Authentication__JwtBearer__Authority", realmUrl)
        .WithEnvironment("Authentication__JwtBearer__Issuer", realmUrl)
        .WithEnvironment("Authentication__JwtBearer__Audience", "hexalith-eventstore")
        .WithEnvironment("Authentication__JwtBearer__RequireHttpsMetadata", FalseLiteral)
        // Explicitly clear SigningKey to prevent dual-mode auth conflict.
        // If SigningKey exists in appsettings/secrets, clearing it ensures
        // ConfigureJwtBearerOptions.cs uses OIDC discovery (Authority) mode only.
        .WithEnvironment("Authentication__JwtBearer__SigningKey", "");
}

// Add Tenants domain service with DAPR sidecar.
// Tenants shares the same state store and pub/sub as EventStore.
IResourceBuilder<ProjectResource> tenants = builder.AddProject<Projects.Hexalith_Tenants>("tenants")
    .WithDaprSidecar(sidecar => sidecar
        .WithOptions(new DaprSidecarOptions {
            AppId = "tenants",
            Config = tenantsAccessControlConfigPath,
        })
        .WithReference(eventStoreResources.StateStore)
        .WithReference(eventStoreResources.PubSub))
    .WithEnvironment("Tenants__BootstrapGlobalAdminUserId", "admin-user");

// Add sample domain service with DAPR sidecar.
// NOTE: sample does NOT reference StateStore or PubSub components.
// Domain services have zero infrastructure access (D4, AC #13).
// Not wiring these references means the sample sidecar doesn't load
// these component definitions at all -- stronger isolation than scoping alone.
IResourceBuilder<ProjectResource> sample = builder.AddProject<Projects.Hexalith_EventStore_Sample>("sample")
    // NOTE: sample does NOT reference Redis, StateStore, or PubSub.
    // Domain services have zero infrastructure access (D4, AC #13).
    // Direct Redis access would bypass DAPR component scoping entirely.
    .WithDaprSidecar(sidecar => sidecar
        .WithOptions(new DaprSidecarOptions {
            AppId = "sample",
            Config = sampleAccessControlConfigPath,
        }));

// Add Blazor UI sample — consumes EventStore via Aspire service discovery (Story 18-6).
// Enables SignalR on EventStore so the hub is active when the Blazor UI is running.
_ = eventStore.WithEnvironment("EventStore__SignalR__Enabled", "true");
EndpointReference eventStoreHttps = eventStore.GetEndpoint("https");
EndpointReference adminServerHttps = adminServer.GetEndpoint("https");
IResourceBuilder<ProjectResource> blazorUi = builder.AddProject<Projects.Hexalith_EventStore_Sample_BlazorUI>("sample-blazor-ui")
    .WithReference(eventStore)
    .WaitFor(eventStore)
    .WithExternalHttpEndpoints()
    // SignalR HubConnectionBuilder bypasses Aspire service discovery (it doesn't use HttpClientFactory),
    // so we must pass the resolved eventstore endpoint URL explicitly.
    .WithEnvironment("EventStore__SignalR__HubUrl", ReferenceExpression.Create($"{eventStoreHttps}/hubs/projection-changes"));

if (keycloak is not null && realmUrl is not null) {
    _ = tenants
        .WithReference(keycloak)
        .WaitFor(keycloak)
        .WithEnvironment("Authentication__JwtBearer__Authority", realmUrl)
        .WithEnvironment("Authentication__JwtBearer__Issuer", realmUrl)
        .WithEnvironment("Authentication__JwtBearer__Audience", "hexalith-eventstore")
        .WithEnvironment("Authentication__JwtBearer__RequireHttpsMetadata", FalseLiteral)
        .WithEnvironment("Authentication__JwtBearer__SigningKey", "");

    _ = adminServer
        .WithReference(keycloak)
        .WaitFor(keycloak)
        .WithEnvironment("Authentication__JwtBearer__Authority", realmUrl)
        .WithEnvironment("Authentication__JwtBearer__Issuer", realmUrl)
        .WithEnvironment("Authentication__JwtBearer__Audience", "hexalith-eventstore")
        .WithEnvironment("Authentication__JwtBearer__RequireHttpsMetadata", FalseLiteral)
        .WithEnvironment("Authentication__JwtBearer__SigningKey", "");

    _ = blazorUi
        .WithReference(keycloak)
        .WaitFor(keycloak)
        .WithEnvironment("EventStore__Authentication__Authority", realmUrl)
        .WithEnvironment("EventStore__Authentication__ClientId", "hexalith-eventstore")
        .WithEnvironment("EventStore__Authentication__Username", "admin-user")
        .WithEnvironment("EventStore__Authentication__Password", "admin-pass");

    _ = adminUI
        .WithReference(keycloak)
        .WaitFor(keycloak)
        .WithEnvironment("EventStore__AdminServer__SwaggerUrl", ReferenceExpression.Create($"{adminServerHttps}/swagger/index.html"))
        .WithEnvironment("EventStore__Authentication__Authority", realmUrl)
        .WithEnvironment("EventStore__Authentication__ClientId", "hexalith-eventstore")
        .WithEnvironment("EventStore__Authentication__Username", "admin-user")
        .WithEnvironment("EventStore__Authentication__Password", "admin-pass");
}
else {
    _ = adminUI.WithEnvironment("EventStore__AdminServer__SwaggerUrl", ReferenceExpression.Create($"{adminServerHttps}/swagger/index.html"));
}

// --- Publisher environments (only activate during `aspire publish`) ---
// Aspire requires exactly one compute environment per resource at publish time.
// Set PUBLISH_TARGET to select the target publisher:
//   PUBLISH_TARGET=docker  -> Docker Compose (docker-compose.yaml + .env)
//   PUBLISH_TARGET=k8s     -> Kubernetes (Helm charts / K8s YAML manifests)
//   PUBLISH_TARGET=aca     -> Azure Container Apps (Bicep modules)
// Example: PUBLISH_TARGET=docker aspire publish -o ./publish-output/docker
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
            })
            .WithReference(eventStoreResources.PubSub))
        .WaitFor(eventStore);

    _ = testSubscriber
        .WithEnvironment("EVENTSTORE_TEST_SUBSCRIBER_TOPIC", testSubscriberTopic)
        .WithEnvironment("EVENTSTORE_TEST_SUBSCRIBER_AUTH_SECRET", testSubscriberAuthSecret);
}

builder.Build().Run();

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

void ForwardEventStoreEnvironment(string configurationKey, string environmentKey) {
    string? value = builder.Configuration[configurationKey];
    if (!string.IsNullOrWhiteSpace(value)) {
        _ = eventStore.WithEnvironment(environmentKey, value);
    }
}
