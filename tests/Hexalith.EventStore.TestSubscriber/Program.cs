using System.Collections.Concurrent;
using System.Text.Json;

const int MaxRetainedEvents = 256;
const string AuthHeaderName = "X-Test-Auth";

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
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

app.MapPost(route, async (HttpRequest request) => {
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
    catch (JsonException ex) {
        return Results.BadRequest(new { error = "Invalid CloudEvent JSON", detail = ex.Message });
    }
    catch (OperationCanceledException) {
        return Results.StatusCode(StatusCodes.Status499ClientClosedRequest);
    }

    string? id = GetString(cloudEvent, "id");
    string? correlationId = GetString(data, "correlationId")
        ?? GetString(data, "CorrelationId")
        ?? ParseCorrelationIdFromCloudEventId(id);
    string? sequence = GetNumberOrString(data, "sequenceNumber")
        ?? GetNumberOrString(data, "SequenceNumber")
        ?? ParseSequenceFromCloudEventId(id);

    receivedEvents.Enqueue(new ReceivedPubSubEvent(
        Topic: topic,
        Type: GetString(cloudEvent, "type"),
        Source: GetString(cloudEvent, "source"),
        Id: id,
        CorrelationId: correlationId,
        SequenceNumber: sequence,
        TenantId: GetString(data, "tenantId") ?? GetString(data, "TenantId"),
        Domain: GetString(data, "domain") ?? GetString(data, "Domain"),
        AggregateId: GetString(data, "aggregateId") ?? GetString(data, "AggregateId"),
        EventTypeName: GetString(data, "eventTypeName") ?? GetString(data, "EventTypeName"),
        ReceivedAt: DateTimeOffset.UtcNow));

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

static string? ParseCorrelationIdFromCloudEventId(string? cloudEventId) {
    if (string.IsNullOrWhiteSpace(cloudEventId)) {
        return null;
    }

    int separator = cloudEventId.LastIndexOf(':');
    return separator <= 0 ? null : cloudEventId[..separator];
}

static string? ParseSequenceFromCloudEventId(string? cloudEventId) {
    if (string.IsNullOrWhiteSpace(cloudEventId)) {
        return null;
    }

    int separator = cloudEventId.LastIndexOf(':');
    if (separator < 0 || separator >= cloudEventId.Length - 1) {
        return null;
    }

    string candidate = cloudEventId[(separator + 1)..];
    return long.TryParse(candidate, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out _)
        ? candidate
        : null;
}

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
