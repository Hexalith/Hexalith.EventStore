using CommunityToolkit.Aspire.Hosting.Dapr;

using Hexalith.EventStore.Aspire;

IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

// Resolve DAPR access control configuration path (Story 5.1, D4, FR34).
// Both commandapi and sample sidecars load this Configuration CRD.
string accessControlConfigPath = Path.Combine(Directory.GetCurrentDirectory(), "DaprComponents", "accesscontrol.yaml");
if (!File.Exists(accessControlConfigPath)) {
    accessControlConfigPath = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "DaprComponents", "accesscontrol.yaml"));
}

if (!File.Exists(accessControlConfigPath)) {
    throw new FileNotFoundException(
        "DAPR access control configuration not found. "
        + "Ensure accesscontrol.yaml exists in the DaprComponents directory (D4, FR34).",
        accessControlConfigPath);
}

// Add EventStore topology using the convenience extension
// launchSettings.json specifies port 8080 to match DAPR AppPort configuration.
IResourceBuilder<ProjectResource> commandApi = builder.AddProject<Projects.Hexalith_EventStore_CommandApi>("commandapi");
HexalithEventStoreResources eventStoreResources = builder.AddHexalithEventStore(commandApi, accessControlConfigPath);

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
            Config = accessControlConfigPath,
        }));

// Add Blazor UI sample — consumes CommandApi via Aspire service discovery (Story 18-6).
// Enables SignalR on CommandApi so the hub is active when the Blazor UI is running.
_ = commandApi.WithEnvironment("EventStore__SignalR__Enabled", "true");
IResourceBuilder<ProjectResource> blazorUi = builder.AddProject<Projects.Hexalith_EventStore_Sample_BlazorUI>("sample-blazor-ui")
    .WithReference(commandApi)
    .WithExternalHttpEndpoints();

if (keycloak is not null && realmUrl is not null) {
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
if (string.Equals(publishTarget, "docker", StringComparison.OrdinalIgnoreCase))
    builder.AddDockerComposeEnvironment("docker");
else if (string.Equals(publishTarget, "k8s", StringComparison.OrdinalIgnoreCase))
    builder.AddKubernetesEnvironment("k8s");
else if (string.Equals(publishTarget, "aca", StringComparison.OrdinalIgnoreCase))
    builder.AddAzureContainerAppEnvironment("aca");

builder.Build().Run();
