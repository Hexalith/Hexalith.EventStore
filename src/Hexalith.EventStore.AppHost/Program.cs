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
if (!string.Equals(builder.Configuration["EnableKeycloak"], "false", StringComparison.OrdinalIgnoreCase)) {
    // Realm-as-code: hexalith-realm.json auto-imported on container start.
    // Port 8180 avoids conflict with commandapi on 8080.
    IResourceBuilder<KeycloakResource> keycloak = builder.AddKeycloak("keycloak", 8180)
        .WithRealmImport("./KeycloakRealms");

    // Wire Keycloak OIDC auth (D11, Story 5.1 Task 8).
    // The existing ConfigureJwtBearerOptions.cs OIDC discovery path handles everything
    // when Authentication:JwtBearer:Authority is set to the Keycloak realm URL.
    EndpointReference keycloakEndpoint = keycloak.GetEndpoint("http");
    var realmUrl = ReferenceExpression.Create($"{keycloakEndpoint}/realms/hexalith");
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

builder.Build().Run();
