using CommunityToolkit.Aspire.Hosting.Dapr;

var builder = DistributedApplication.CreateBuilder(args);

// Infrastructure
var redis = builder.AddRedis("redis");

// Services
var commandApi = builder.AddProject<Projects.Hexalith_EventStore_CommandApi>("commandapi")
    .WithDaprSidecar(new DaprSidecarOptions { AppId = "commandapi" })
    .WithReference(redis);

var sample = builder.AddProject<Projects.Hexalith_EventStore_Sample>("sample")
    .WithDaprSidecar(new DaprSidecarOptions { AppId = "sample" })
    .WithReference(redis);

builder.Build().Run();
