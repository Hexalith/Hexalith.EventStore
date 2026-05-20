using System.Text.Json;
using System.Text.Json.Serialization;

using Hexalith.EventStore.Admin.Abstractions.Models;
using Hexalith.EventStore.Admin.Abstractions.Models.Streams;

namespace Hexalith.EventStore.Admin.Cli.Formatting;

internal sealed class EventDetailJsonConverter : JsonConverter<EventDetail> {
    public override EventDetail? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        using var doc = JsonDocument.ParseValue(ref reader);
        JsonElement root = doc.RootElement;

        bool hasPayloadDescriptor = root.TryGetProperty("payload", out JsonElement payloadElement)
            && payloadElement.ValueKind is not JsonValueKind.Null;
        bool hasPayloadJson = root.TryGetProperty("payloadJson", out JsonElement payloadJsonElement)
            && payloadJsonElement.ValueKind is not JsonValueKind.Null;

        if (!hasPayloadDescriptor && !hasPayloadJson) {
            throw new JsonException("EventDetail JSON must include either 'payload' (descriptor) or 'payloadJson' (raw).");
        }

        EventDetail detail = new(
            GetRequiredString(root, "tenantId"),
            GetRequiredString(root, "domain"),
            GetRequiredString(root, "aggregateId"),
            GetRequiredInt64(root, "sequenceNumber"),
            GetRequiredString(root, "eventTypeName"),
            GetRequiredDateTimeOffset(root, "timestamp"),
            GetRequiredString(root, "correlationId"),
            GetOptionalString(root, "causationId"),
            GetOptionalString(root, "userId"),
            hasPayloadJson ? payloadJsonElement.GetString() ?? string.Empty : string.Empty);

        if (hasPayloadDescriptor) {
            if (payloadElement.ValueKind is not JsonValueKind.Object) {
                throw new JsonException("EventDetail 'payload' must be an object (AdminRedactedContent descriptor).");
            }

            AdminRedactedContent? descriptor;
            try {
                descriptor = payloadElement.Deserialize<AdminRedactedContent>(options);
            }
            catch (JsonException ex) {
                throw new JsonException("EventDetail 'payload' descriptor is malformed.", ex);
            }

            if (descriptor is null) {
                throw new JsonException("EventDetail 'payload' descriptor deserialized to null.");
            }

            detail = detail with {
                PayloadJson = null!,
                Payload = descriptor,
            };
        }

        return detail;
    }

    public override void Write(Utf8JsonWriter writer, EventDetail value, JsonSerializerOptions options) {
        writer.WriteStartObject();
        writer.WriteString("tenantId", value.TenantId);
        writer.WriteString("domain", value.Domain);
        writer.WriteString("aggregateId", value.AggregateId);
        writer.WriteNumber("sequenceNumber", value.SequenceNumber);
        writer.WriteString("eventTypeName", value.EventTypeName);
        writer.WriteString("timestamp", value.Timestamp);
        writer.WriteString("correlationId", value.CorrelationId);
        WriteOptionalString(writer, "causationId", value.CausationId);
        WriteOptionalString(writer, "userId", value.UserId);
        if (value.Payload is not null) {
            writer.WritePropertyName("payload");
            JsonSerializer.Serialize(writer, value.Payload, options);
        }
        else {
            WriteOptionalString(writer, "payloadJson", value.PayloadJson);
        }

        writer.WriteEndObject();
    }

    private static string GetRequiredString(JsonElement root, string propertyName)
        => root.TryGetProperty(propertyName, out JsonElement value) && value.GetString() is string text
            ? text
            : throw new JsonException($"Required property '{propertyName}' is missing.");

    private static string? GetOptionalString(JsonElement root, string propertyName)
        => root.TryGetProperty(propertyName, out JsonElement value) && value.ValueKind is not JsonValueKind.Null
            ? value.GetString()
            : null;

    private static long GetRequiredInt64(JsonElement root, string propertyName)
        => root.TryGetProperty(propertyName, out JsonElement value) && value.TryGetInt64(out long number)
            ? number
            : throw new JsonException($"Required property '{propertyName}' is missing.");

    private static DateTimeOffset GetRequiredDateTimeOffset(JsonElement root, string propertyName)
        => root.TryGetProperty(propertyName, out JsonElement value) && value.TryGetDateTimeOffset(out DateTimeOffset timestamp)
            ? timestamp
            : throw new JsonException($"Required property '{propertyName}' is missing.");

    private static void WriteOptionalString(Utf8JsonWriter writer, string propertyName, string? value) {
        if (value is not null) {
            writer.WriteString(propertyName, value);
        }
    }
}
