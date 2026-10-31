using Dapr.Actors.Runtime;

using Hexalith.EventStore.Operations.Actors;
using Hexalith.EventStore.Operations.Configuration;
using Hexalith.EventStore.Operations.Endpoints;
using Hexalith.EventStore.Operations.Replay;
using Hexalith.EventStore.Operations.Routing;
using Hexalith.EventStore.Operations.Security;
using Hexalith.EventStore.Operations.Telemetry;
using Hexalith.EventStore.ServiceDefaults;
using Hexalith.EventStore.ServiceDefaults.Authentication;

using Microsoft.Extensions.Options;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
_ = DaprAppChannelSecurity.ValidateConfiguration(
    builder.Environment,
    builder.Configuration[DaprAppChannelSecurity.ConfigurationKey]);

builder.AddServiceDefaults();
builder.Services.AddDaprClient();
builder.Services.AddOperationsHttpSecurity();
builder.Services.AddDevelopmentOperationsCatalog(builder.Configuration);
builder.Services.AddHttpClient();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<EventStoreOperationsTelemetry>();
builder.Services.AddSingleton<IDeadLetterReplayTransport, DaprDeadLetterReplayTransport>();
builder.Services.AddSingleton<IValidateOptions<EventStoreOperationsOptions>, EventStoreOperationsOptionsValidator>();
_ = builder.Services
    .AddOptions<EventStoreOperationsOptions>()
    .Bind(builder.Configuration.GetSection(EventStoreOperationsOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddActors(actorOptions =>
    actorOptions.Actors.RegisterActor<DeadLetterDrainActor>(DeadLetterDrainActor.ActorTypeName));
builder.Services.AddHostedService<DeadLetterBacklogReconciler>();

WebApplication app = builder.Build();
app.UseRouting();
app.UseEventStoreHttpHostSecurity();
app.UseAuthentication();
app.UseAuthorization();
app.UseOperationsHumanRelayScope();
app.UseDevelopmentOperationsCatalog();

app.MapDefaultEndpoints();
app.MapDeadLetterOperations();
app.MapSubscribeHandler().RequireEventStoreChannel();
app.MapActorsHandlers().RequireEventStoreChannel();

await app.RunAsync().ConfigureAwait(false);

/// <summary>
/// Entry point exposed for focused host tests.
/// </summary>
public partial class Program;
