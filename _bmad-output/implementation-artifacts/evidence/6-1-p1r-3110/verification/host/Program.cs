using System.Security.Cryptography;
using Dapr.Actors;
using Dapr.Actors.Client;
using Dapr.Client;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Commands;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton(new DaprClientBuilder().Build());
builder.Services.AddEventStoreServer(builder.Configuration);
builder.Services.AddSingleton<ICommandStatusStore, DaprCommandStatusStore>();
WebApplication app = builder.Build();
app.UseRouting();
app.MapActorsHandlers();
app.MapGet("/ready", () => Results.Ok(new { ready = true }));
app.MapGet("/identity", () => Results.Ok(AppDomain.CurrentDomain.GetAssemblies()
    .Where(a => !a.IsDynamic && a.GetName().Name?.StartsWith("Hexalith.EventStore", StringComparison.Ordinal) == true)
    .Select(a => new { name = a.GetName().Name, version = a.GetName().Version?.ToString(), sha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(a.Location))) }).ToArray()));
app.MapPost("/command", async (CommandEnvelope command) =>
{
    var proxy = ActorProxy.Create<IAggregateActor>(new ActorId(command.AggregateIdentity.ActorId), "AggregateActor");
    return Results.Ok(await proxy.ProcessCommandAsync(command));
});
app.MapPost("/status/{tenant}/{message}", async (string tenant, string message, CommandStatusRecord status, ICommandStatusStore store) =>
{
    await store.WriteStatusAsync(tenant, message, status);
    return Results.Ok(new { written = true });
});
app.MapGet("/status/{tenant}/{message}", async (string tenant, string message, ICommandStatusStore store)
    => Results.Ok(await store.ReadStatusAsync(tenant, message)));
app.MapGet("/sequence/{tenant}/{aggregate}", async (string tenant, string aggregate) =>
{
    var proxy = ActorProxy.Create<IAggregateActor>(new ActorId($"{tenant}:counter:{aggregate}"), "AggregateActor");
    return Results.Ok(await proxy.GetCurrentSequenceAsync());
});
app.Run();
