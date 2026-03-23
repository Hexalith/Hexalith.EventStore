using CommunityToolkit.Aspire.Hosting.Dapr;

using Hexalith.EventStore.AppHost;
using Hexalith.EventStore.Aspire;

PrerequisiteValidator.Validate();

IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

// Resolve DAPR access control configuration paths (D4, FR34).
// Each receiving service now has its own Configuration CRD so policies apply only
// to the intended inbound surface instead of every sidecar sharing one config file.
string commandApiAccessControlConfigPath = ResolveDaprConfigPath("accesscontrol.yaml");
string adminServerAccessControlConfigPath = ResolveDaprConfigPath("accesscontrol.admin-server.yaml");
string sampleAccessControlConfigPath = ResolveDaprConfigPath("accesscontrol.sample.yaml");

// Add EventStore topology using the convenience extension
// launchSettings.json specifies port 8080 to match DAPR AppPort configuration.
IResourceBuilder<ProjectResource> commandApi = builder.AddProject<Projects.Hexalith_EventStore_CommandApi>("commandapi");
IResourceBuilder<ProjectResource> adminServer = builder.AddProject<Projects.Hexalith_EventStore_Admin_Server_Host>("admin-server");
HexalithEventStoreResources eventStoreResources = builder.AddHexalithEventStore(
    commandApi,
    adminServer,
    commandApiAccessControlConfigPath,
    adminServerAccessControlConfigPath);

// Keycloak identity provider for E2E security testing (D11, Story 5.1 Task 8).
// Enabled by default for local development with real OIDC token testing.
// Set EnableKeycloak=false in environment or appsettings to run without Keycloak
// (falls back to symmetric key auth via Authentication:JwtBearer:SigningKey).
IResourceBuilder<KeycloakResource>? keycloak = null;
ReferenceExpression? realmUrl = null;
if (!string.Equals(builder.Configuration["EnableKeycloak"], "false", StringComparison.OrdinalIgnoreCase)) {
    // Realm-as-code: hexalith-realm.json auto-imported on container start.
    // Port 8180 avoids conflict with commandapi on 8080.
    keycloak = builder.AddKeycloak("keycloak", 8180)
        .WithRealmImport("./KeycloakRealms");

    // Wire Keycloak OIDC auth (D11, Story 5.1 Task 8).
    // The existing ConfigureJwtBearerOptions.cs OIDC discovery path handles everything
    // when Authentication:JwtBearer:Authority is set to the Keycloak realm URL.
    EndpointReference keycloakEndpoint = keycloak.GetEndpoint("http");
    realmUrl = ReferenceExpression.Create($"{keycloakEndpoint}/realms/hexalith");
    _ = commandApi
        .WithReference(keycloak)
        .WaitFor(keycloak)
        .WithEnvironment("Authentication__JwtBearer__Authority", realmUrl)
        .WithEnvironment("Authentication__JwtBearer__Issuer", realmUrl)
        .WithEnvironment("Authentication__JwtBearer__Audience", "hexalith-eventstore")
        .WithEnvironment("Authentication__JwtBearer__RequireHttpsMetadata", "false")
        // Explicitly clear SigningKey to prevent dual-mode auth conflict.
        // If SigningKey exists in appsettings/secrets, clearing it ensures
        // ConfigureJwtBearerOptions.cs uses OIDC discovery (Authority) mode only.
        .WithEnvironment("Authentication__JwtBearer__SigningKey", "");
}

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

// Add Blazor UI sample — consumes CommandApi via Aspire service discovery (Story 18-6).
// Enables SignalR on CommandApi so the hub is active when the Blazor UI is running.
_ = commandApi.WithEnvironment("EventStore__SignalR__Enabled", "true");
EndpointReference commandApiHttps = commandApi.GetEndpoint("https");
IResourceBuilder<ProjectResource> blazorUi = builder.AddProject<Projects.Hexalith_EventStore_Sample_BlazorUI>("sample-blazor-ui")
    .WithReference(commandApi)
    .WaitFor(commandApi)
    .WithExternalHttpEndpoints()
    // SignalR HubConnectionBuilder bypasses Aspire service discovery (it doesn't use HttpClientFactory),
    // so we must pass the resolved commandapi endpoint URL explicitly.
    .WithEnvironment("EventStore__SignalR__HubUrl", ReferenceExpression.Create($"{commandApiHttps}/hubs/projection-changes"));

if (keycloak is not null && realmUrl is not null) {
    _ = adminServer
        .WithReference(keycloak)
        .WaitFor(keycloak)
        .WithEnvironment("Authentication__JwtBearer__Authority", realmUrl)
        .WithEnvironment("Authentication__JwtBearer__Issuer", realmUrl)
        .WithEnvironment("Authentication__JwtBearer__Audience", "hexalith-eventstore")
        .WithEnvironment("Authentication__JwtBearer__RequireHttpsMetadata", "false")
        .WithEnvironment("Authentication__JwtBearer__SigningKey", "");

    _ = blazorUi
        .WithReference(keycloak)
        .WaitFor(keycloak)
        .WithEnvironment("EventStore__Authentication__Authority", realmUrl)
        .WithEnvironment("EventStore__Authentication__ClientId", "hexalith-eventstore")
        .WithEnvironment("EventStore__Authentication__Username", "admin-user")
        .WithEnvironment("EventStore__Authentication__Password", "admin-pass");
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
