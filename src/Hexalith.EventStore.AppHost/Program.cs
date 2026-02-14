using CommunityToolkit.Aspire.Hosting.Dapr;
using Hexalith.EventStore.Aspire;

var builder = DistributedApplication.CreateBuilder(args);

// Resolve DAPR access control configuration path (Story 5.1, D4, FR34).
// Both commandapi and sample sidecars load this Configuration CRD.
var accessControlConfigPath = Path.Combine(Directory.GetCurrentDirectory(), "DaprComponents", "accesscontrol.yaml");
if (!File.Exists(accessControlConfigPath))
{
    accessControlConfigPath = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "DaprComponents", "accesscontrol.yaml"));
}

if (!File.Exists(accessControlConfigPath))
{
    throw new FileNotFoundException(
        "DAPR access control configuration not found. "
        + "Ensure accesscontrol.yaml exists in the DaprComponents directory (D4, FR34).",
        accessControlConfigPath);
}

// Add EventStore topology using the convenience extension
var commandApi = builder.AddProject<Projects.Hexalith_EventStore_CommandApi>("commandapi")
    // DAPR sidecar for commandapi is configured with AppPort=8080.
    // Keep ASP.NET Core listening port aligned to avoid ERR_DIRECT_INVOKE.
    .WithEnvironment("ASPNETCORE_URLS", "http://localhost:8080");
var eventStoreResources = builder.AddHexalithEventStore(commandApi, accessControlConfigPath);

// Add sample domain service with DAPR sidecar.
// NOTE: sample does NOT reference StateStore or PubSub components.
// Domain services have zero infrastructure access (D4, AC #13).
// Not wiring these references means the sample sidecar doesn't load
// these component definitions at all -- stronger isolation than scoping alone.
var sample = builder.AddProject<Projects.Hexalith_EventStore_Sample>("sample")
    // DAPR sidecar for sample is configured with AppPort=8081.
    // Keep ASP.NET Core listening port aligned to avoid ERR_DIRECT_INVOKE.
    .WithEnvironment("ASPNETCORE_URLS", "http://localhost:8081")
    .WithReference(eventStoreResources.Redis)
    .WaitFor(eventStoreResources.Redis)
    .WithDaprSidecar(sidecar => sidecar
        .WithOptions(new DaprSidecarOptions
        {
            AppId = "sample",
            AppPort = 8081,
            Config = accessControlConfigPath,
        }));

builder.Build().Run();
