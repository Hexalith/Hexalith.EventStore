#if SELECTED
using System.Text.Json;
using Hexalith.EventStore.Client.Reminders;
using Hexalith.EventStore.Contracts.Effects;
using Hexalith.EventStore.Contracts.Reminders;

namespace PublishedConsumer;

/// <summary>Re-folds the committed schedule event on every registration/callback; a second committed event consumes it.</summary>
public sealed class StreamReminderSource : IReminderIntentSource
{
    /// <inheritdoc/>
    public async Task<IReadOnlyList<ReminderIntent>> GetCurrentIntentsAsync(ReminderTarget target, CancellationToken cancellationToken = default)
    {
        string actor = target.Tenant + ":" + target.Domain + ":" + target.Aggregate;
        using var http = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:" + Environment.GetEnvironmentVariable("DAPR_HTTP_PORT")), Timeout = TimeSpan.FromSeconds(20) };
        string prefix = "/v1.0/actors/AggregateActor/" + Uri.EscapeDataString(actor) + "/state/";
        string rawMetadata = await http.GetStringAsync(prefix + Uri.EscapeDataString(actor + ":metadata"), cancellationToken);
        using JsonDocument metadata = JsonDocument.Parse(rawMetadata);
        if (metadata.RootElement.GetProperty("currentSequence").GetInt64() != 1) return [];
        string rawEvent = await http.GetStringAsync(prefix + Uri.EscapeDataString(actor + ":events:1"), cancellationToken);
        using JsonDocument streamEvent = JsonDocument.Parse(rawEvent);
        CounterIncremented value = JsonSerializer.Deserialize<CounterIncremented>(streamEvent.RootElement.GetProperty("payload").GetBytesFromBase64(), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        if (value.DueUtc is null) return [];
        return [new ReminderIntent(target.Tenant, target.Domain, target.Aggregate, value.DueUtc.Value, EffectKindCatalog.DateResume,
            "p1r.resume", "{}"u8.ToArray(), target.Domain, target.Aggregate, 1, 1)
            { SourceMessageId = streamEvent.RootElement.GetProperty("messageId").GetString() }];
    }

    /// <inheritdoc/>
    public ReminderCommand TranslateDueIntent(ReminderIntent intent) => new("Increment", intent.Payload);
}
#endif
