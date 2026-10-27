using System.Text.Json;

using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.DomainService;

using PublishedConsumer;

if (args.Contains("--identity", StringComparer.Ordinal))
{
    Console.WriteLine("P1R_RESULT " + JsonSerializer.Serialize(new { identities = ConsumerIdentity.Read() }, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    return;
}

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Configuration["EventStore:Actors:AggregateActorTypeName"] = "AggregateActor";
builder.Configuration["EventStore:Projections:WorkersEnabled"] = "false";
builder.Configuration["EventStore:IdempotencyAdmission:Enabled"] = "true";
builder.Configuration["EventStore:IdempotencyAdmission:ActiveDigestKeyVersion"] = "fixture-v1";
builder.Configuration["EventStore:IdempotencyAdmission:DigestKeySource"] = "Configuration";
builder.Configuration["EventStore:IdempotencyAdmission:DigestKeys:fixture-v1"] = "MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=";
builder.Services.AddSingleton(new Dapr.Client.DaprClientBuilder().Build());
builder.Services.AddEventStoreServer(builder.Configuration);
builder.Services.AddSingleton<Hexalith.EventStore.Server.Commands.ICommandStatusStore, Hexalith.EventStore.Server.Commands.DaprCommandStatusStore>();
#if FLOOR
builder.Services.AddSingleton<Hexalith.EventStore.DomainService.IIdempotencyIntentAdapter, FixtureIntentAdapter>();
builder.Services.AddSingleton<Hexalith.EventStore.Server.Commands.ITrustedEffectAdmissionPolicy, FixtureEffectPolicy>();
#endif
#if SELECTED
ManifestEvolutionFixture.Register(builder.Services);
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<ProductionRefusalObserver>();
builder.Services.AddActors(options => options.Actors.RegisterActor<FixtureQueryActor>("P1RQueryActor"));
builder.Services.Configure<Hexalith.EventStore.Server.Identity.IdentityAdmissionSigningOptions>(options =>
    options.PrivateKeyPem = File.ReadAllText(Environment.GetEnvironmentVariable("P1R_QUERY_SIGNING_KEY_PATH")!));
builder.Services.AddSingleton<Hexalith.EventStore.Server.Identity.IIdentityAdmissionSigner, Hexalith.EventStore.Server.Identity.IdentityAdmissionSigner>();
builder.Configuration["EventStore:DataProtection:PersistToStateStore"] = "true";
builder.Configuration["EventStore:DataProtection:StateKey"] = "p1r:cursor-keys";
builder.Services.AddEventStoreDataProtection(builder.Configuration, "p1r-counter");
ArgumentException.ThrowIfNullOrWhiteSpace(builder.Configuration["APP_API_TOKEN"]);
builder.Configuration["EventStore:Reminders:ActorTypeName"] = "P1RReminderActor";
builder.Configuration["EventStore:Reminders:Workload"] = "p1r-fixture";
builder.Configuration["EventStore:Reminders:Purposes:" + Hexalith.EventStore.Contracts.Effects.EffectKindCatalog.DateResume] = "qualification";
builder.Configuration["EventStore:Reminders:ReconciliationEnabled"] = "false";
builder.Services.AddEventStoreReminders<StreamReminderSource>();
builder.Services.AddSingleton<Hexalith.EventStore.Client.Reminders.IReminderDelegationTokenProvider, FixtureReminderDelegation>();
builder.Services.AddSingleton<Hexalith.EventStore.Client.Effects.ITrustedEffectSubmitter, FixtureEffectSubmitter>();
#endif
builder.Services.AddSingleton<HttpDomainInvoker>();
builder.Services.AddSingleton<IDomainServiceInvoker>(provider => provider.GetRequiredService<HttpDomainInvoker>());
builder.Services.AddSingleton<IEventPublisher, BoundedEventPublisher>();
builder.Services.AddSingleton<BoundedDeadLetterPublisher>();
builder.Services.AddSingleton<IDeadLetterPublisher>(provider => provider.GetRequiredService<BoundedDeadLetterPublisher>());
WebApplication app = builder.Build();
#if SELECTED
app.Services.GetRequiredService<ProductionRefusalObserver>().Start();
#endif
app.MapActorsHandlers();
CapabilityEndpoints.Map(app);
#if SELECTED
app.MapEventStoreReminders();
ReminderEndpoints.Map(app);
QueryBoundaryEndpoints.Map(app);
app.MapPost("/evolution", async (JsonElement input, Dapr.Client.DaprClient client) =>
{
    string key = "p1r:evolution:" + Guid.NewGuid().ToString("N");
    byte[] original = input.GetProperty("payload").GetBytesFromBase64();
    await client.SaveStateAsync("statestore", key, original);
    byte[] before = await client.GetStateAsync<byte[]>("statestore", key);
    using var domain = new HttpClient { BaseAddress = new Uri(Environment.GetEnvironmentVariable("P1R_DOMAIN_URL")!), Timeout = TimeSpan.FromSeconds(30) };
    using var response = await domain.PostAsJsonAsync("/evolution", new { payload = Convert.ToBase64String(before) });
    response.EnsureSuccessStatusCode();
    using var result = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    byte[] after = await client.GetStateAsync<byte[]>("statestore", key);
    return Results.Json(new { domain = result.RootElement.Clone(), fixture = key,
        beforeHash = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(before)),
        afterHash = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(after)),
        persistedBytesUnchanged = original.SequenceEqual(before) && before.SequenceEqual(after) });
});
app.MapGet("/manifest", () => new { fingerprint = ManifestEvolutionFixture.Fingerprint, domain = "d", fromVersion = 1, currentVersion = 2, registered = true });
app.MapGet("/manifest-refusal/{actorId}", (string actorId, ProductionRefusalObserver observer) =>
    observer.Read(actorId) is JsonElement value ? Results.Json(value) : Results.NotFound());
#endif
app.MapGet("/identity", () => new { identities = ConsumerIdentity.Read() });
app.MapPost("/status-fixture/{message}", async (string message, JsonElement input, Hexalith.EventStore.Server.Commands.ICommandStatusStore store) =>
{
    bool? retryable = input.GetProperty("retryable").ValueKind == JsonValueKind.Null ? null : input.GetProperty("retryable").GetBoolean();
    var wire = JsonSerializer.SerializeToElement(new { status = Hexalith.EventStore.Contracts.Commands.CommandStatus.Completed,
        timestamp = DateTimeOffset.UnixEpoch, aggregateId = "fixture-counter", eventCount = 1, messageId = message,
        correlationId = "corr", retryable, recoveryReasonCode = retryable is null ? null : "p1r-fixture", drainAttemptCount = retryable is null ? (int?)null : 1 });
    var status = wire.Deserialize<Hexalith.EventStore.Contracts.Commands.CommandStatusRecord>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    await store.WriteStatusAsync("tenant-a", message, status);
    return Results.Json(await store.ReadStatusAsync("tenant-a", message));
});
app.MapGet("/status-fixture/{message}", async (string message, Hexalith.EventStore.Server.Commands.ICommandStatusStore store) =>
    Results.Json(await store.ReadStatusAsync("tenant-a", message)));
app.MapGet("/reader/{actorId}", async (string actorId, ILogger<EventStreamReader> logger) =>
{
    string[] address = actorId.Split(':');
    try
    {
        var result = await new EventStreamReader(DaprStateReaderProxy.Create(actorId), logger)
            .RehydrateAsync(new Hexalith.EventStore.Contracts.Identity.AggregateIdentity(address[0], address[1], address[2]));
        return Results.Json(new { ok = true, sequence = result?.CurrentSequence ?? 0, count = result?.Events.Count ?? 0 });
    }
    catch (Exception error) { return Results.Json(new { ok = false, error = error.GetType().FullName, message = error.Message }); }
});
app.MapPost("/writer-admission/{actorId}", async (string actorId, ILogger<EventPersister> logger) =>
{
    string[] address = actorId.Split(':');
    var identity = new Hexalith.EventStore.Contracts.Identity.AggregateIdentity(address[0], address[1], address[2]);
    var state = DaprStateReaderProxy.Create(actorId);
    var protection = System.Reflection.DispatchProxy.Create<IEventPayloadProtectionService, ProtectionProbeProxy>();
    var positions = new PositionProbe();
    string? errorType = null;
    try
    {
        await new EventPersister(state, logger, protection, positions).PersistEventsAsync(identity, "counter",
            new CommandEnvelope("probe", address[0], address[1], address[2], "Increment", "{}"u8.ToArray(), "corr", "cause", "user", null),
            Hexalith.EventStore.Contracts.Results.DomainResult.Success([new CounterIncremented()]), "v1");
    }
    catch (Exception error) { errorType = error.GetType().FullName; }
    return Results.Json(new { refused = errorType is not null, error = errorType,
        protectionCalls = ((ProtectionProbeProxy)protection).Calls, allocations = positions.Calls, staging = ((DaprStateReaderProxy)state).StagingCalls });
});
app.MapPost("/execute/{actorId}", async (string actorId, JsonElement request, HttpDomainInvoker domain, BoundedDeadLetterPublisher deadLetters) =>
{
    string[] address = actorId.Split(':');
    var command = new CommandEnvelope("message-" + Guid.NewGuid().ToString("N"), address[0], address[1], address[2],
        request.GetProperty("append").GetBoolean() ? "Increment" : "Inspect", "{}"u8.ToArray(), "correlation", "causation", "fixture-user", null);
    try
    {
        CommandProcessingResult result = await Proxy(actorId).ProcessCommandAsync(command);
        string raw = await ReadStateAsync(actorId, actorId + ":metadata") ?? "null";
        using JsonDocument metadata = JsonDocument.Parse(raw);
        return Results.Json(new { ok = result.Accepted, count = domain.Observations.GetValueOrDefault(actorId, -1),
            sequence = metadata.RootElement.ValueKind == JsonValueKind.Null ? 0 : metadata.RootElement.GetProperty("currentSequence").GetInt64(),
            metadata = metadata.RootElement.Clone(), error = result.Accepted ? null : "CommandProcessingRefusal", message = result.ErrorMessage,
            deadLetterBookkeeping = deadLetters.Count });
    }
    catch (Exception error) { return Results.Json(new { ok = false, error = error.GetType().FullName, message = error.Message }); }
});
app.MapPost("/fixture/{actorId}", async (string actorId, JsonElement request) =>
{
    foreach (JsonElement operation in request.EnumerateArray())
        if (!operation.GetProperty("key").GetString()!.StartsWith(actorId + ":", StringComparison.Ordinal)) return Results.BadRequest();
    _ = await Proxy(actorId).GetCurrentSequenceAsync();
    var operations = request.EnumerateArray().Select(operation => new
    {
        operation = operation.GetProperty("operation").GetString(),
        request = new { key = operation.GetProperty("key").GetString(), value = operation.TryGetProperty("value", out JsonElement value) ? value.Clone() : (JsonElement?)null },
    }).ToArray();
    using var http = Sidecar();
    using var response = await http.PostAsJsonAsync("/v1.0/actors/AggregateActor/" + Uri.EscapeDataString(actorId) + "/state", operations);
    if (!response.IsSuccessStatusCode) throw new InvalidOperationException("Dapr fixture transaction failed: " + response.StatusCode + ":" + await response.Content.ReadAsStringAsync());
    return Results.Ok();
});
app.MapGet("/read/{actorId}/{key}", async (string actorId, string key) =>
{
    string? value = await ReadStateAsync(actorId, key);
    return value is null ? Results.NotFound() : Results.Text(value, "application/json");
});
app.Run();

static IAggregateActor Proxy(string actorId) => new ActorProxyFactory(new ActorProxyOptions
{
    HttpEndpoint = "http://127.0.0.1:" + Environment.GetEnvironmentVariable("DAPR_HTTP_PORT"),
    RequestTimeout = TimeSpan.FromSeconds(35),
}).CreateActorProxy<IAggregateActor>(new ActorId(actorId), "AggregateActor");

static HttpClient Sidecar() => new() { BaseAddress = new Uri("http://127.0.0.1:" + Environment.GetEnvironmentVariable("DAPR_HTTP_PORT")), Timeout = TimeSpan.FromSeconds(30) };

static async Task<string?> ReadStateAsync(string actorId, string key)
{
    using var http = Sidecar();
    using var response = await http.GetAsync("/v1.0/actors/AggregateActor/" + Uri.EscapeDataString(actorId) + "/state/" + Uri.EscapeDataString(key));
    if (response.StatusCode == System.Net.HttpStatusCode.NoContent || response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
    response.EnsureSuccessStatusCode();
    return await response.Content.ReadAsStringAsync();
}
