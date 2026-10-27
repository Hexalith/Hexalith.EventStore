#if SELECTED
using System.Text.Json;
using Dapr.Actors;
using Dapr.Actors.Client;
using Dapr.Client;
using Hexalith.EventStore.Client.Reminders;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Reminders;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Server.Actors;

namespace PublishedConsumer;

/// <summary>Registers a real stream-derived Reminder and reads its persisted natural-delivery disposition.</summary>
public static class ReminderEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/reminder/{aggregate}", async (string aggregate, JsonElement input, IReminderRegistrar registrar) =>
        {
            var target = new ReminderTarget("tenant-a", "counter", aggregate);
            var actor = new ActorProxyFactory(new ActorProxyOptions { HttpEndpoint = "http://127.0.0.1:" + Environment.GetEnvironmentVariable("DAPR_HTTP_PORT") })
                .CreateActorProxy<IAggregateActor>(new ActorId("tenant-a:counter:" + aggregate), "AggregateActor");
            var command = new CommandEnvelope("schedule-" + aggregate, "tenant-a", "counter", aggregate, "Schedule",
                JsonSerializer.SerializeToUtf8Bytes(new { dueUtc = input.GetProperty("dueUtc").GetDateTimeOffset() }), "corr", "cause", "fixture-user", null);
            CommandProcessingResult accepted = await actor.ProcessCommandAsync(command);
            var result = await registrar.ConvergeAsync(target);
            var intents = await new StreamReminderSource().GetCurrentIntentsAsync(target);
            return Results.Json(new { accepted = accepted.Accepted, armed = result.Armed, submitted = result.Submitted,
                reminderName = ReminderIdentityCodec.ComputeReminderName(intents.Single()), actorId = ReminderIdentityCodec.ComputeActorId("tenant-a", aggregate) });
        });
        app.MapGet("/reminder/{aggregate}/{name}", async (string aggregate, string name, DaprClient client) =>
        {
            string actorId = ReminderIdentityCodec.ComputeActorId("tenant-a", aggregate);
            string itemKey = "eventstore:reminders:v1:P1RReminderActor:item:" + actorId;
            JsonElement? disposition = await client.GetStateAsync<JsonElement?>("statestore", itemKey + ":disposition:" + name);
            bool submitted = disposition is JsonElement value && value.ValueKind == JsonValueKind.Object
                && value.TryGetProperty("disposition", out JsonElement status) && status.GetString() == "Submitted";
            return Results.Json(new { submitted = submitted ? 1 : 0, disposition,
                state = await client.GetStateAsync<JsonElement?>("statestore", itemKey) });
        });
    }
}
#endif
