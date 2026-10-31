using Hexalith.EventStore.Sharding.TestHost;

RunBinding binding = RunBinding.LoadHost(Environment.GetEnvironmentVariable(RunBinding.BindingVariable), AppContext.BaseDirectory);
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton(binding);
builder.Services.AddSingleton<RunFence>();
builder.Services.AddActors(options =>
{
    options.UseJsonSerialization = true;
    options.Actors.RegisterActor<ShardActor>(ShardActor.ActorType);
});
WebApplication app = builder.Build();
app.MapGet("/alive", () => Results.Ok(new { binding.RunId, ProcessId = Environment.ProcessId }));
app.MapActorsHandlers();
await app.RunAsync();
