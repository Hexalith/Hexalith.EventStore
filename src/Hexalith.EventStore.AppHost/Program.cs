using CommunityToolkit.Aspire.Hosting.Dapr;

var builder = DistributedApplication.CreateBuilder(args);

// Infrastructure
var redis = builder.AddRedis("redis");

// Services
// Note: DAPR components (statestore, pubsub) are auto-discovered from DaprComponents/ directory
// by the Dapr sidecar at runtime. CommunityToolkit.Aspire.Hosting.Dapr v13 deprecated explicit
// AddDaprStateStore/AddDaprPubSub + WithReference calls in favor of file-based convention discovery.
var commandApi = builder.AddProject<Projects.Hexalith_EventStore_CommandApi>("commandapi")
    .WithDaprSidecar(new DaprSidecarOptions { AppId = "commandapi" })
    .WithReference(redis);

var sample = builder.AddProject<Projects.Hexalith_EventStore_Sample>("sample")
    .WithDaprSidecar(new DaprSidecarOptions { AppId = "sample" })
    .WithReference(redis);

builder.Build().Run();
