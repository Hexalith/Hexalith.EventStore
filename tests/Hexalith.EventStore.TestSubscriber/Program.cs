using System.Collections.Concurrent;
using System.Security.Cryptography;

using Dapr.Client;
using System.Text.Json;

const int MaxRetainedEvents = 256;
const string EffectStore = "subscriber-proof-state";
const string AuthHeaderName = "X-Test-Auth";

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton(_ => new DaprClientBuilder().Build());
WebApplication app = builder.Build();

string topic = (Environment.GetEnvironmentVariable("EVENTSTORE_TEST_SUBSCRIBER_TOPIC")?.Trim()) is { Length: > 0 } configuredTopic
    ? configuredTopic
    : "tenant-a.counter.events";
string route = "/events/" + topic.Replace('.', '-');
string? authSecret = Environment.GetEnvironmentVariable("EVENTSTORE_TEST_SUBSCRIBER_AUTH_SECRET")?.Trim();
authSecret = string.IsNullOrEmpty(authSecret) ? null : authSecret;

ConcurrentQueue<ReceivedPubSubEvent> receivedEvents = new();

app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

app.MapGet("/dapr/subscribe", () => Results.Json(new[] {
    new {
        pubsubname = "pubsub",
        topic,
        route,
    },
}));

app.MapPost(route, async (HttpRequest request, DaprClient dapr) => {
    JsonElement cloudEvent;
    JsonElement data;
    try {
        using JsonDocument document = await JsonDocument.ParseAsync(
            request.Body,
            cancellationToken: request.HttpContext.RequestAborted)
            .ConfigureAwait(false);
        cloudEvent = document.RootElement.Clone();
        data = cloudEvent.TryGetProperty("data", out JsonElement dataElement)
            ? dataElement.Clone()
            : default;
    }
    catch (JsonException) {
        return Results.BadRequest();
    }
    catch (OperationCanceledException) {
        return Results.StatusCode(StatusCodes.Status499ClientClosedRequest);
    }

    string? id = GetString(cloudEvent, "id");
    string? source = GetString(cloudEvent, "source");
    string? persistedId = GetString(data, "messageId") ?? GetString(data, "MessageId");
    if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(source) || id != persistedId)
    {
        return Results.BadRequest();
    }

    var delivered = new ReceivedPubSubEvent(
        Topic: topic,
        Type: GetString(cloudEvent, "type"),
        Source: source,
        Id: id,
        CorrelationId: GetString(data, "correlationId") ?? GetString(data, "CorrelationId"),
        SequenceNumber: GetNumberOrString(data, "sequenceNumber") ?? GetNumberOrString(data, "SequenceNumber"),
        TenantId: GetString(data, "tenantId") ?? GetString(data, "TenantId"),
        Domain: GetString(data, "domain") ?? GetString(data, "Domain"),
        AggregateId: GetString(data, "aggregateId") ?? GetString(data, "AggregateId"),
        EventTypeName: GetString(data, "eventTypeName") ?? GetString(data, "EventTypeName"),
        ReceivedAt: DateTimeOffset.UtcNow);
    string effectKey = BuildEffectKey(topic, source, id);
    CancellationToken cancellationToken = request.HttpContext.RequestAborted;
    ReceivedPubSubEvent? effect = await dapr.GetStateAsync<ReceivedPubSubEvent?>(EffectStore, effectKey,
        ConsistencyMode.Strong, cancellationToken: cancellationToken).ConfigureAwait(false);
    if (effect is null)
    {
        try
        {
            _ = await dapr.TrySaveStateAsync(EffectStore, effectKey, delivered, string.Empty,
                new StateOptions { Concurrency = ConcurrencyMode.FirstWrite, Consistency = ConsistencyMode.Strong },
                new Dictionary<string, string>(StringComparer.Ordinal) { ["ttlInSeconds"] = "3600" }, cancellationToken).ConfigureAwait(false);
        }
        catch (Dapr.DaprException) when (!cancellationToken.IsCancellationRequested)
        {
            // A concurrent delivery may have committed the one logical effect.
        }

        effect = await dapr.GetStateAsync<ReceivedPubSubEvent?>(EffectStore, effectKey,
            ConsistencyMode.Strong, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    if (effect?.Id != id || effect.Source != source)
    {
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }

    receivedEvents.Enqueue(delivered);

    // Bounded queue: drop oldest when over the cap so a long-running fixture cannot leak memory
    // and so DAPR at-least-once redeliveries from prior tests cannot accumulate without bound.
    while (receivedEvents.Count > MaxRetainedEvents && receivedEvents.TryDequeue(out _)) {
    }

    return Results.Ok();
});

app.MapGet("/events", (HttpRequest request, string? correlationId) => {
    if (!IsAuthorized(request, authSecret)) {
        return Results.Unauthorized();
    }

    IEnumerable<ReceivedPubSubEvent> events = receivedEvents;
    if (!string.IsNullOrWhiteSpace(correlationId)) {
        events = events.Where(e => string.Equals(e.CorrelationId, correlationId, StringComparison.Ordinal));
    }

    return Results.Ok(events.OrderBy(e => e.ReceivedAt).ToArray());
});

app.MapGet("/effects", async (HttpRequest request, string source, string id, DaprClient dapr) =>
{
    if (!IsAuthorized(request, authSecret))
    {
        return Results.Unauthorized();
    }

    ReceivedPubSubEvent? effect = await dapr.GetStateAsync<ReceivedPubSubEvent?>(EffectStore, BuildEffectKey(topic, source, id),
        ConsistencyMode.Strong, cancellationToken: request.HttpContext.RequestAborted).ConfigureAwait(false);
    return effect is null ? Results.NotFound() : Results.Ok(effect);
});

app.MapDelete("/events", (HttpRequest request, string? correlationId) => {
    if (!IsAuthorized(request, authSecret)) {
        return Results.Unauthorized();
    }

    if (string.IsNullOrWhiteSpace(correlationId)) {
        // Tests should always scope clears to their own correlation id; the unscoped clear
        // is retained for emergency reset only.
        while (receivedEvents.TryDequeue(out _)) {
        }

        return Results.NoContent();
    }

    // Scoped clear: filter to non-matching events and rebuild the queue. Late at-least-once
    // redeliveries that share another test's correlation id stay intact for that test.
    ReceivedPubSubEvent[] retained = receivedEvents
        .Where(e => !string.Equals(e.CorrelationId, correlationId, StringComparison.Ordinal))
        .OrderBy(e => e.ReceivedAt)
        .ToArray();
    while (receivedEvents.TryDequeue(out _)) {
    }

    foreach (ReceivedPubSubEvent retainedEvent in retained) {
        receivedEvents.Enqueue(retainedEvent);
    }

    return Results.NoContent();
});

app.Run();

static bool IsAuthorized(HttpRequest request, string? expectedSecret) {
    if (string.IsNullOrEmpty(expectedSecret)) {
        return true; // Auth disabled when no secret is configured (legacy / unscoped runs).
    }

    return request.Headers.TryGetValue(AuthHeaderName, out Microsoft.Extensions.Primitives.StringValues headerValue)
        && string.Equals(headerValue.ToString(), expectedSecret, StringComparison.Ordinal);
}

static string? GetString(JsonElement element, string propertyName) {
    if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out JsonElement property)) {
        return null;
    }

    return property.ValueKind == JsonValueKind.String ? property.GetString() : property.ToString();
}

static string? GetNumberOrString(JsonElement element, string propertyName) {
    if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out JsonElement property)) {
        return null;
    }

    return property.ValueKind switch {
        JsonValueKind.Number => property.GetInt64().ToString(System.Globalization.CultureInfo.InvariantCulture),
        JsonValueKind.String => property.GetString(),
        _ => property.ToString(),
    };
}

static string BuildEffectKey(string topic, string source, string id)
    => "effect:" + Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new[] { topic, source, id })));

internal sealed record ReceivedPubSubEvent(
    string Topic,
    string? Type,
    string? Source,
    string? Id,
    string? CorrelationId,
    string? SequenceNumber,
    string? TenantId,
    string? Domain,
    string? AggregateId,
    string? EventTypeName,
    DateTimeOffset ReceivedAt);
