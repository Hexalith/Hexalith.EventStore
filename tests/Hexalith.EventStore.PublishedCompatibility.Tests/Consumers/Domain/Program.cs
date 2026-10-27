using System.Text.Json;

using Hexalith.EventStore.Client.Registration;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Results;

using PublishedConsumer;

if (args.Contains("--identity", StringComparer.Ordinal))
{
    Console.WriteLine("P1R_RESULT " + JsonSerializer.Serialize(new { identities = ConsumerIdentity.Read() }, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    return;
}

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Services.AddScoped<CounterProcessor>();
builder.Services.AddEventStoreClient<CounterProcessor>();
#if SELECTED
builder.Services.AddEventStoreClient<EvolutionAggregate>();
builder.Services.AddKnownEventPayload<EvolvedIncremented>();
builder.Services.AddEventPayloadUpcaster<IncrementUpcaster>();
builder.Services.AddSingleton(new Dapr.Client.DaprClientBuilder().Build());
builder.Services.AddEventStoreReadModelStore();
builder.Services.Configure<Hexalith.EventStore.Client.Security.IdentityAdmissionOptions>(options =>
{
    options.VerificationKeyPem = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(Environment.GetEnvironmentVariable("P1R_QUERY_VERIFICATION_KEY")!));
    options.AuthorityRevision = 1;
});
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
builder.Services.AddSingleton<Hexalith.EventStore.Client.Security.IIdentityAdmissionProof, Hexalith.EventStore.Client.Security.IdentityAdmissionProof>();
builder.Services.AddScoped<Hexalith.EventStore.DomainService.IDomainQueryHandler, AuthorizedCounterQueryHandler>();
#endif
WebApplication app = builder.Build();
app.MapGet("/identity", () => new { identities = ConsumerIdentity.Read() });
#if SELECTED
app.MapGet("/stats", () => new { applyCalls = CounterState.ApplyCalls, handleCalls = CounterProcessor.HandleCalls, queryReads = AuthorizedCounterQueryHandler.Reads });
app.MapPost("/query", async (Hexalith.EventStore.Contracts.Queries.QueryEnvelope query, HttpContext context) =>
    await Hexalith.EventStore.DomainService.DomainQueryDispatcher.ExecuteAsync(context.RequestServices, query, context.RequestAborted));
#else
app.MapGet("/stats", () => new { applyCalls = CounterState.ApplyCalls, handleCalls = CounterProcessor.HandleCalls });
#endif
#if SELECTED
app.MapPost("/evolution", async (JsonElement input, EvolutionAggregate aggregate) =>
{
    byte[] original = input.GetProperty("payload").GetBytesFromBase64();
    byte[] before = original.ToArray();
    var metadata = new Hexalith.EventStore.Contracts.Events.EventMetadata("message", "item", "counter", "tenant-a", "counter", 1, 987,
        DateTimeOffset.UnixEpoch, "corr", "cause", "fixture-user", "v1", typeof(EvolvedIncremented).FullName!, 1, "json") { PayloadVersion = 1 };
    var current = new DomainServiceCurrentState(null, [new Hexalith.EventStore.Contracts.Events.EventEnvelope(metadata, original, null)], 0, 1);
    var command = new CommandEnvelope("inspect", "tenant-a", "counter", "item", nameof(InspectEvolution), "{}"u8.ToArray(), "corr", "cause", "fixture-user", null);
    try
    {
        await aggregate.ProcessAsync(command, current);
        return Results.Json(new { ok = true, count = aggregate.ObservedCount, inputUnchanged = before.SequenceEqual(original),
            beforeHash = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(before)),
            afterHash = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(original)), registryInjected = true });
    }
    catch (Exception error) { return Results.Json(new { ok = false, error = error.GetType().FullName, message = error.Message }); }
});
#endif
app.MapPost("/process", async (DomainServiceRequest request, CounterProcessor processor) =>
{
    try
    {
        DomainResult result = await processor.ProcessAsync(request.Command, request.CurrentState);
        return Results.Json(new { ok = true, count = processor.ObservedCount, append = result.Events.Count,
            events = result.Events.Cast<CounterIncremented>().ToArray() });
    }
    catch (Exception error)
    {
        return Results.Json(new { ok = false, error = error.GetType().FullName, message = error.Message,
            applyCalls = CounterState.ApplyCalls, handleCalls = CounterProcessor.HandleCalls });
    }
});
app.Run();
