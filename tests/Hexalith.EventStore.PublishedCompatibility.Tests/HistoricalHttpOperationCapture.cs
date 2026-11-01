using System.Text;
using System.Text.Json;

namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Captures finite public test-operation coordinates without retaining signed cursors or additional proof material.</summary>
internal static class HistoricalHttpOperationCapture
{
    internal static JsonElement Request(object request)
    {
        var input = JsonSerializer.SerializeToElement(request);
        if (input.ValueKind != JsonValueKind.Object) return JsonSerializer.SerializeToElement(new { unsupported = true });
        return JsonSerializer.SerializeToElement(new
        {
            append = Boolean(input, "append"),
            mode = Choice(input, "mode", ["valid", "missing", "forged", "foreign-tenant", "foreign-actor", "foreign-domain", "changed-filter", "changed-query", "tampered"]),
            serializer = Choice(input, "serializer", ["json", "xml"]), route = Choice(input, "route", ["declared-handler"]),
            position = Number(input, "position"), sequence = Number(input, "sequence"), cursorHash = Digest(input, "cursor"),
        });
    }

    internal static JsonElement? Response(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body); var value = document.RootElement;
            if (value.ValueKind != JsonValueKind.Object) return null;
            return JsonSerializer.SerializeToElement(new
            {
                declaration = value.TryGetProperty("declared", out _) ? JsonSerializer.SerializeToElement(new
                {
                    declared = Names(value, "declared"), persisted = Names(value, "persisted"),
                    registry = Choice(value, "registry", ["Hexalith.EventStore.Queries.DaprDomainQueryHandlerRegistry"]),
                    supported = Boolean(value, "supported"), unknown = Boolean(value, "unknown"),
                }) : (JsonElement?)null,
                decoded = Boolean(value, "decoded"), success = Boolean(value, "success"),
                position = value.TryGetProperty("position", out var position) && position.ValueKind == JsonValueKind.String ? position.GetString() : null,
                watermark = Number(value, "watermark"), sequence = Number(value, "sequence"), count = Number(value, "count"),
                modelWatermark = value.TryGetProperty("model", out var model) && model.ValueKind == JsonValueKind.Object ? Number(model, "watermark") : null,
                cursorHash = Digest(value, "cursor"),
                payloadHash = value.TryGetProperty("payload", out var payload) && payload.ValueKind == JsonValueKind.Object
                    ? QualificationRuntime.Hash(Encoding.UTF8.GetBytes(payload.GetRawText())) : null,
            });
        }
        catch (JsonException) { return null; }
    }

    private static string[]? Names(JsonElement value, string key)
        => value.TryGetProperty(key, out var field) && field.ValueKind == JsonValueKind.Array
            ? field.EnumerateArray().Select(item => item.ValueKind == JsonValueKind.String && item.GetString() == "get-counter" ? "get-counter" : "unsupported").ToArray() : null;

    private static string? Choice(JsonElement value, string key, string[] allowed)
        => value.TryGetProperty(key, out var field) ? field.ValueKind == JsonValueKind.String
            && allowed.Contains(field.GetString(), StringComparer.Ordinal) ? field.GetString() : "unsupported" : null;

    private static long? Number(JsonElement value, string key)
        => value.TryGetProperty(key, out var field) && field.ValueKind == JsonValueKind.Number && field.TryGetInt64(out long number) ? number : null;

    private static bool? Boolean(JsonElement value, string key)
        => value.TryGetProperty(key, out var field) && field.ValueKind is JsonValueKind.True or JsonValueKind.False ? field.GetBoolean() : null;

    private static string? Digest(JsonElement value, string key)
        => value.TryGetProperty(key, out var field) && field.ValueKind == JsonValueKind.String
            ? QualificationRuntime.Hash(Encoding.UTF8.GetBytes(field.GetString()!)) : null;
}
