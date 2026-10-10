using Dapr.Actors;
using Dapr.Actors.Client;
using Dapr.Client;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Configuration;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton(new DaprClientBuilder().Build());
builder.Services.AddEventStoreServer(builder.Configuration);
builder.Services.AddSingleton<ICommandStatusStore, DaprCommandStatusStore>();
#if P1R_CAPABILITIES
QualificationCapabilities.Configure(builder);
#endif
WebApplication app = builder.Build();
app.UseRouting();
app.MapActorsHandlers();
app.MapGet("/ready", () => Results.Ok(new { ready = true }));
app.MapGet("/qualification-hold", async (HttpContext context) =>
{
    await Task.Delay(Timeout.Infinite, context.RequestAborted);
    return Results.Ok(new { released = true });
});
app.MapGet("/identity", () => Results.Ok(PublishedIdentity.Read()));
app.MapPost("/status/{tenant}/{message}", async (string tenant, string message, CommandStatusRecord status, ICommandStatusStore store) =>
{
    await store.WriteStatusAsync(tenant, message, status);
    return Results.Ok(new { written = true });
});
app.MapGet("/status/{tenant}/{message}", async (string tenant, string message, ICommandStatusStore store)
    => Results.Ok(await store.ReadStatusAsync(tenant, message)));
#if P1R_CAPABILITIES
QualificationCapabilities.Map(app);
#endif
app.MapPost("/command", async (CommandEnvelope command) =>
{
    IAggregateActor proxy = ActorProxy.Create<IAggregateActor>(new ActorId(command.AggregateIdentity.ActorId), "AggregateActor");
    return Results.Ok(await proxy.ProcessCommandAsync(command));
});
app.MapGet("/sequence/{tenant}/{aggregate}", async (string tenant, string aggregate) =>
{
    IAggregateActor proxy = ActorProxy.Create<IAggregateActor>(new ActorId($"{tenant}:counter:{aggregate}"), "AggregateActor");
    return Results.Ok(await proxy.GetCurrentSequenceAsync());
});
app.Run();
