using System.Collections.Concurrent;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
WebApplication app = builder.Build();

var receivedEvents = new ConcurrentQueue<ReceivedPubSubEvent>();

app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

app.MapGet("/dapr/subscribe", () => Results.Json(new[] {
    new {
        pubsubname = "pubsub",
        topic = "tenant-a.counter.events",
        route = "/events/tenant-a-counter",
    },
}));

app.MapPost("/events/tenant-a-counter", async (HttpRequest request) => {
    using JsonDocument document = await JsonDocument.ParseAsync(request.Body, cancellationToken: request.HttpContext.RequestAborted)
        .ConfigureAwait(false);
    JsonElement cloudEvent = document.RootElement.Clone();
    JsonElement data = cloudEvent.TryGetProperty("data", out JsonElement dataElement)
        ? dataElement.Clone()
        : default;

    string? correlationId = GetString(data, "correlationId") ?? GetString(data, "CorrelationId");
    string? sequence = GetNumberOrString(data, "sequenceNumber") ?? GetNumberOrString(data, "SequenceNumber");

    receivedEvents.Enqueue(new ReceivedPubSubEvent(
        Topic: "tenant-a.counter.events",
        Type: GetString(cloudEvent, "type"),
        Source: GetString(cloudEvent, "source"),
        Id: GetString(cloudEvent, "id"),
        CorrelationId: correlationId,
        SequenceNumber: sequence,
        TenantId: GetString(data, "tenantId") ?? GetString(data, "TenantId"),
        Domain: GetString(data, "domain") ?? GetString(data, "Domain"),
        AggregateId: GetString(data, "aggregateId") ?? GetString(data, "AggregateId"),
        EventTypeName: GetString(data, "eventTypeName") ?? GetString(data, "EventTypeName"),
        ReceivedAt: DateTimeOffset.UtcNow));

    return Results.Ok();
});

app.MapGet("/events", (string? correlationId) => {
    IEnumerable<ReceivedPubSubEvent> events = receivedEvents;
    if (!string.IsNullOrWhiteSpace(correlationId)) {
        events = events.Where(e => string.Equals(e.CorrelationId, correlationId, StringComparison.Ordinal));
    }

    return Results.Ok(events.OrderBy(e => e.ReceivedAt).ToArray());
});

app.MapDelete("/events", () => {
    while (receivedEvents.TryDequeue(out _)) {
    }

    return Results.NoContent();
});

app.Run();

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
