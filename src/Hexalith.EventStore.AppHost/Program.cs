using CommunityToolkit.Aspire.Hosting.Dapr;
using Hexalith.EventStore.Aspire;

var builder = DistributedApplication.CreateBuilder(args);

// Add EventStore topology using the convenience extension
var commandApi = builder.AddProject<Projects.Hexalith_EventStore_CommandApi>("commandapi");
var eventStoreResources = builder.AddHexalithEventStore(commandApi);

// Add sample domain service with DAPR sidecar
var sample = builder.AddProject<Projects.Hexalith_EventStore_Sample>("sample")
    .WithReference(eventStoreResources.Redis)
    .WaitFor(eventStoreResources.Redis)
    .WithDaprSidecar(sidecar => sidecar
        .WithOptions(new DaprSidecarOptions { AppId = "sample", AppPort = 8081 })
        .WithReference(eventStoreResources.StateStore)
        .WithReference(eventStoreResources.PubSub));

builder.Build().Run();
